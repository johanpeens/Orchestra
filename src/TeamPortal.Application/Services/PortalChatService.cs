using System.Globalization;
using System.Text;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Application.Services;

public sealed class PortalChatService : IChatService
{
    private readonly IPortalExportService _export;

    /// <summary>
    /// Words that carry no signal in a question about a process. Scoring on them
    /// makes every process look equally relevant, because they appear in all of
    /// them.
    /// </summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "with",
        "is", "are", "was", "were", "be", "do", "does", "did", "what", "which",
        "who", "whom", "whose", "how", "when", "where", "why", "can", "i", "we",
        "you", "me", "my", "our", "it", "this", "that", "there", "here", "at",
        "by", "from", "about", "tell", "show", "list", "give", "please", "need",
        "want", "know", "s", "process", "processes", "step", "steps"
    };

    public PortalChatService(IPortalExportService export) => _export = export;

    /// <inheritdoc />
    /// <remarks>
    /// History is accepted and ignored. This one matches words against the
    /// export; it has no notion of "it" to resolve, and pretending otherwise by
    /// concatenating the previous question would only make the scoring worse.
    /// </remarks>
    public async Task<ChatReplyDto> AskAsync(
        string question,
        IReadOnlyList<ChatMessageDto>? history = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new ChatReplyDto(Greeting(), []);
        }

        PortalExportDto portal = await _export.ExportAsync(ct: ct).ConfigureAwait(false);

        IReadOnlyList<string> terms = Terms(question);

        if (terms.Count == 0)
        {
            return new ChatReplyDto(Overview(portal), []);
        }


        // Somebody named in the question is almost always the subject of it.
        // Checked before the unit filter and before ranking, because a person's
        // name also appears in the steps they own - ranking would answer "who is
        // David Chen" with a description of a process he happens to touch.
        if (MatchedPerson(portal, question) is { } person)
        {
            return AboutPerson(portal, person);
        }

        // "Who is in Underwriting" is a roster question, not a process one.
        if (AsksWho(question) && MatchedUnit(portal, terms) is { } peopleUnit)
        {
            return UnitRoster(portal, peopleUnit);
        }

        // "Which processes does Sales own" is a filter over a business unit, not
        // a lookup of one process. Ranking it found the best-matching Sales
        // process and described that - a confident answer to a question nobody
        // asked. Checked before the ranking for exactly that reason.
        if (AsksToList(question) && MatchedUnit(portal, terms) is { } unit)
        {
            return ByUnit(portal, unit);
        }

        // "Who does X" is a different question from "what is X", and answering
        // it with a step list is answering something else.
        if (AsksWho(question))
        {
            return People(portal, terms);
        }

        IReadOnlyList<Hit> hits = Rank(portal, terms);

        if (hits.Count == 0)
        {
            return new ChatReplyDto(NothingFound(question, portal), []);
        }

        return Describe(hits, AsksToSee(question));
    }

    // --- Retrieval ------------------------------------------------------------

    /// <summary>One process, how well it matched, and which of its steps matched.</summary>
    private sealed record Hit(
        ExportFolderDto Folder,
        ExportProcessDto Process,
        int Score,
        IReadOnlyList<ExportStepDto> MatchingSteps);

    /// <summary>
    /// Scores every process against the question's terms. A hit on the process
    /// name is worth most - it is the thing somebody names when they ask - then
    /// its department, then its description, then anything in its steps.
    /// </summary>
    private static IReadOnlyList<Hit> Rank(PortalExportDto portal, IReadOnlyList<string> terms)
    {
        List<Hit> hits = [];

        foreach (ExportFolderDto folder in portal.Folders)
        {
            foreach (ExportProcessDto process in folder.Processes)
            {
                int score = 0;

                score += 10 * Matches(process.Name, terms);
                score += 6 * Matches(folder.Name, terms);
                score += 5 * Matches(process.Department, terms);
                score += 3 * Matches(process.Description, terms);

                List<ExportStepDto> matchingSteps = [];

                foreach (ExportStepDto step in process.Steps)
                {
                    int stepScore =
                        4 * Matches(step.Name, terms)
                        + 2 * Matches(step.Description, terms)
                        + 2 * Matches(step.Department, terms)
                        + 2 * Matches(string.Join(' ', step.ResponsiblePeople), terms);

                    if (stepScore > 0)
                    {
                        matchingSteps.Add(step);
                        score += stepScore;
                    }
                }

                if (score > 0)
                {
                    hits.Add(new Hit(folder, process, score, matchingSteps));
                }
            }
        }

        return [.. hits.OrderByDescending(h => h.Score).ThenBy(h => h.Process.Position)];
    }

    /// <summary>
    /// How many of the question's terms this text contains. Substring, not whole
    /// word, so "onboard" finds "Onboarding" without a stemmer.
    /// </summary>
    private static int Matches(string? text, IReadOnlyList<string> terms) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : terms.Count(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Terms(string question) =>
    [
        .. question
            .Split(
                [' ', '\t', '\n', '\r', ',', '.', '?', '!', ';', ':', '"', '\'', '(', ')', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 1 && !Noise.Contains(w))
            .Select(w => w.ToLower(CultureInfo.CurrentCulture))
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    /// Whether the question wants a list rather than one thing. These words are
    /// stripped as noise before scoring, so this reads the raw question.
    /// </summary>
    private static bool AsksToList(string question) =>
        question.Contains("which", StringComparison.OrdinalIgnoreCase)
        || question.Contains("list", StringComparison.OrdinalIgnoreCase)
        || question.Contains("all ", StringComparison.OrdinalIgnoreCase)
        || question.Contains("what processes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The business unit the question names, if it names one. Best match wins, so
    /// "business enablement" beats a unit that happens to share one word.
    /// </summary>
    private static ExportDepartmentDto? MatchedUnit(
        PortalExportDto portal, IReadOnlyList<string> terms) =>
        portal.Departments
            .Select(d => (Unit: d, Score: Matches(d.Name, terms)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Unit)
            .FirstOrDefault();

    private static bool AsksWho(string question) =>
        question.Contains("who", StringComparison.OrdinalIgnoreCase)
        || question.Contains("responsible", StringComparison.OrdinalIgnoreCase)
        || question.Contains("owns", StringComparison.OrdinalIgnoreCase)
        || question.Contains("owner", StringComparison.OrdinalIgnoreCase);

    // --- Answers --------------------------------------------------------------

    /// <summary>
    /// Whether the asker wants to LOOK at something. Screenshots are attached
    /// only then: an answer decorated with a picture nobody asked for is noise,
    /// and the pictures are the heaviest thing on the page.
    /// </summary>
    private static bool AsksToSee(string question) =>
        question.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
        || question.Contains("show", StringComparison.OrdinalIgnoreCase)
        || question.Contains("look like", StringComparison.OrdinalIgnoreCase)
        || question.Contains("picture", StringComparison.OrdinalIgnoreCase)
        || question.Contains("image", StringComparison.OrdinalIgnoreCase)
        || question.Contains("see ", StringComparison.OrdinalIgnoreCase);

    private static ChatReplyDto Describe(IReadOnlyList<Hit> hits, bool withScreenshots)
    {
        Hit best = hits[0];
        StringBuilder sb = new();

        sb.Append(best.Process.Name)
          .Append(" sits in ")
          .Append(best.Folder.Name)
          .Append(" at position ")
          .Append(best.Process.Position.ToString(CultureInfo.CurrentCulture));

        if (!string.IsNullOrWhiteSpace(best.Process.Department))
        {
            sb.Append(", owned by ").Append(best.Process.Department);
        }

        sb.Append(". Status: ").Append(best.Process.Status).Append('.');

        if (!string.IsNullOrWhiteSpace(best.Process.Description))
        {
            sb.Append("\n\n").Append(best.Process.Description);
        }

        if (best.Process.Steps.Count > 0)
        {
            sb.Append("\n\nIts ")
              .Append(Plural(best.Process.Steps.Count, "step", "steps"))
              .Append(':');

            foreach (ExportStepDto step in best.Process.Steps)
            {
                sb.Append("\n  ")
                  .Append(step.Number.ToString(CultureInfo.CurrentCulture))
                  .Append(". ")
                  .Append(step.Name);

                if (step.ResponsiblePeople.Count > 0)
                {
                    sb.Append(" - ").Append(string.Join(", ", step.ResponsiblePeople));
                }
            }
        }
        else
        {
            sb.Append("\n\nNo steps have been written up for it yet.");
        }

        // The rest are named, not expanded. Somebody who asked about one thing
        // does not want five processes pasted at them - but they do want to know
        // the question was ambiguous.
        if (hits.Count > 1)
        {
            sb.Append("\n\nAlso matched: ")
              .Append(string.Join(", ", hits.Skip(1).Take(4).Select(h => h.Process.Name)))
              .Append('.');
        }

        IReadOnlyList<ChatImageDto> images = withScreenshots ? Screenshots(best) : [];

        return new ChatReplyDto(sb.ToString(), Sources(hits)) { Images = images };
    }

    /// <summary>
    /// Every screenshot on the process being described, capped. More than a few
    /// stops being an illustration and becomes a gallery.
    /// </summary>
    private static IReadOnlyList<ChatImageDto> Screenshots(Hit hit) =>
    [
        .. hit.Process.Steps
            .SelectMany(step => step.Screenshots.Select(shot => new ChatImageDto(
                hit.Process.Name, step.Name, shot.FileName, shot.Url)))
            .Take(3)
    ];

    private static ChatReplyDto People(PortalExportDto portal, IReadOnlyList<string> terms)
    {
        IReadOnlyList<Hit> hits = Rank(portal, terms);

        if (hits.Count == 0)
        {
            return new ChatReplyDto(
                "I could not find a process matching that, so I cannot say who is "
                + "responsible for it. Try naming the process or the business unit.",
                []);
        }

        Hit best = hits[0];
        StringBuilder sb = new();

        // Steps carry the owners; a process has people only through its steps.
        List<string> everyone =
        [
            .. best.Process.Steps
                .SelectMany(s => s.ResponsiblePeople)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCulture)
        ];

        if (everyone.Count == 0)
        {
            sb.Append("Nobody is assigned to ")
              .Append(best.Process.Name)
              .Append(" yet.");

            if (!string.IsNullOrWhiteSpace(best.Process.Department))
            {
                sb.Append(" It belongs to ").Append(best.Process.Department).Append('.');
            }

            return new ChatReplyDto(sb.ToString(), Sources(hits));
        }

        sb.Append(Plural(everyone.Count, "One person is", $"{everyone.Count} people are"))
          .Append(" on ")
          .Append(best.Process.Name)
          .Append(":\n");

        foreach (string person in everyone)
        {
            // Which steps each of them actually holds - "involved somewhere" is
            // not an answer anybody can act on.
            IEnumerable<string> theirSteps = best.Process.Steps
                .Where(s => s.ResponsiblePeople.Contains(person, StringComparer.OrdinalIgnoreCase))
                .Select(s => s.Number.ToString(CultureInfo.CurrentCulture));

            sb.Append("\n  ")
              .Append(person)
              .Append(" - step ")
              .Append(string.Join(", ", theirSteps));
        }

        return new ChatReplyDto(sb.ToString(), Sources(hits));
    }

    private static ChatReplyDto ByUnit(PortalExportDto portal, ExportDepartmentDto unit)
    {
        List<Hit> owned =
        [
            .. portal.Folders.SelectMany(folder => folder.Processes
                .Where(p => string.Equals(p.Department, unit.Name, StringComparison.OrdinalIgnoreCase))
                .Select(p => new Hit(folder, p, 1, [])))
        ];

        if (owned.Count == 0)
        {
            return new ChatReplyDto(
                $"{unit.Name} does not own any processes yet.", []);
        }

        StringBuilder sb = new();

        sb.Append(unit.Name)
          .Append(" owns ")
          .Append(Plural(owned.Count, "1 process", $"{owned.Count} processes"))
          .Append(':');

        foreach (Hit hit in owned.OrderBy(h => h.Folder.Name, StringComparer.CurrentCulture)
                                 .ThenBy(h => h.Process.Position))
        {
            sb.Append("\n  ")
              .Append(hit.Process.Name)
              .Append(" - ")
              .Append(hit.Folder.Name)
              .Append(", step ")
              .Append(hit.Process.Position.ToString(CultureInfo.CurrentCulture))
              .Append(" (")
              .Append(hit.Process.Status)
              .Append(')');
        }

        return new ChatReplyDto(sb.ToString(), Sources(owned));
    }

    // --- People ---------------------------------------------------------------

    /// <summary>
    /// The person the question names, if it names one. Matched on the whole name
    /// and on each part of it, so "David Chen", "David" and "Chen" all find him.
    /// Longest match wins, so "David Chen" beats another David.
    /// </summary>
    private static ExportPersonDto? MatchedPerson(PortalExportDto portal, string question) =>
        portal.People
            .Select(person => (Person: person, Length: NameHitLength(person.FullName, question)))
            .Where(x => x.Length > 0)
            .OrderByDescending(x => x.Length)
            .Select(x => x.Person)
            .FirstOrDefault();

    /// <summary>
    /// How much of a name the question contains, or zero. Parts shorter than
    /// three characters are ignored - a two-letter name would match half the
    /// dictionary.
    /// </summary>
    private static int NameHitLength(string fullName, string question)
    {
        if (question.Contains(fullName, StringComparison.OrdinalIgnoreCase))
        {
            return fullName.Length;
        }

        return fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part.Length >= 3
                && question.Contains(part, StringComparison.OrdinalIgnoreCase))
            .Select(part => part.Length)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static ChatReplyDto AboutPerson(PortalExportDto portal, ExportPersonDto person)
    {
        StringBuilder sb = new();

        sb.Append(person.FullName);

        if (!string.IsNullOrWhiteSpace(person.JobTitle))
        {
            sb.Append(" is ").Append(person.JobTitle);
        }

        sb.Append(" in ").Append(person.Department).Append('.');

        // Everything they are on the hook for. A name and a job title is a
        // directory entry; what they own is the reason somebody asked.
        List<Hit> theirs =
        [
            .. portal.Folders.SelectMany(folder => folder.Processes
                .Select(p => new Hit(
                    folder,
                    p,
                    1,
                    [.. p.Steps.Where(step => Owns(step, person))]))
                .Where(hit => hit.MatchingSteps.Count > 0))
        ];

        if (theirs.Count == 0)
        {
            sb.Append("\n\nThey are not assigned to any documented step.");

            return new ChatReplyDto(sb.ToString(), []);
        }

        sb.Append("\n\nAssigned to ")
          .Append(Plural(theirs.Sum(t => t.MatchingSteps.Count), "1 step", $"{theirs.Sum(t => t.MatchingSteps.Count)} steps"))
          .Append(':');

        foreach (Hit hit in theirs)
        {
            foreach (ExportStepDto step in hit.MatchingSteps)
            {
                sb.Append("\n  ")
                  .Append(hit.Process.Name)
                  .Append(" step ")
                  .Append(step.Number.ToString(CultureInfo.CurrentCulture))
                  .Append(" - ")
                  .Append(step.Name);
            }
        }

        return new ChatReplyDto(sb.ToString(), Sources(theirs));
    }

    /// <summary>
    /// Whether a step names this person. The step carries owners as display
    /// strings - "Sarah (Project Manager)" - so the name is matched inside them
    /// rather than compared whole.
    /// </summary>
    private static bool Owns(ExportStepDto step, ExportPersonDto person) =>
        step.ResponsiblePeople.Any(owner =>
            owner.Contains(person.FullName, StringComparison.OrdinalIgnoreCase));

    private static ChatReplyDto UnitRoster(PortalExportDto portal, ExportDepartmentDto unit)
    {
        List<ExportPersonDto> members =
        [
            .. portal.People
                .Where(p => string.Equals(p.Department, unit.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.FullName, StringComparer.CurrentCulture)
        ];

        if (members.Count == 0)
        {
            return new ChatReplyDto($"Nobody is listed under {unit.Name}.", []);
        }

        StringBuilder sb = new();

        sb.Append(unit.Name)
          .Append(" has ")
          .Append(Plural(members.Count, "1 person", $"{members.Count} people"))
          .Append(':');

        foreach (ExportPersonDto member in members)
        {
            sb.Append("\n  ").Append(member.FullName);

            if (!string.IsNullOrWhiteSpace(member.JobTitle))
            {
                sb.Append(" - ").Append(member.JobTitle);
            }
        }

        return new ChatReplyDto(sb.ToString(), []);
    }

    private static IReadOnlyList<ChatSourceDto> Sources(IReadOnlyList<Hit> hits) =>
    [
        .. hits.Take(5).Select(h =>
            new ChatSourceDto(h.Folder.Id, h.Folder.Name, h.Process.Id, h.Process.Name))
    ];

    private static string NothingFound(string question, PortalExportDto portal)
    {
        // Saying what IS here beats saying no twice. Somebody who missed by a
        // word can correct themselves from the folder names.
        string folders = string.Join(", ", portal.Folders.Select(f => f.Name));

        return $"I could not find anything about that in the portal.\n\n"
            + $"There {Plural(portal.FolderCount, "is 1 area", $"are {portal.FolderCount} areas")} "
            + $"written up: {folders}. Ask me about one of those, about a business "
            + "unit, or about who is responsible for something.";
    }

    private static string Overview(PortalExportDto portal) =>
        $"There are {portal.ProcessCount} processes across {portal.FolderCount} areas, "
        + $"{portal.StepCount} steps in total.\n\n"
        + $"Areas: {string.Join(", ", portal.Folders.Select(f => f.Name))}.";

    private static string Greeting() =>
        "Ask me about a process, a business unit, a person on the team, or who "
        + "is responsible for something. I answer from what is written up in the portal, so if it "
        + "is not in there I will say so rather than guess.";

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;
}

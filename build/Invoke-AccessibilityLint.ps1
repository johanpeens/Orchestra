<#
********************************************************************************
Purpose : CI gate 7. Every MudIconButton needs an accessible label, and
          aria-hidden may not be applied without a written justification.
          See Frontend/TeamConventions.md and Frontend/CI.md.
Ticket  : SD-PENDING
********************************************************************************
Date            By                      Action
********************************************************************************
23 Aug 2026     Orchestra Team          Created
********************************************************************************
#>

[CmdletBinding()]
param(
    [string] $Root
)

$ErrorActionPreference = 'Stop'

# Three-argument Join-Path is PowerShell 7+. Nest the calls so the script also
# runs on Windows PowerShell 5.1, which is what most developers have locally.
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path (Join-Path $PSScriptRoot '..') 'src'
}

Write-Host 'Gate 7 - accessibility scan...'


# Never lint generated output. obj/ and bin/ contain scaffolded CSS bundles and
# compiler intermediates that are not ours to fix.
function Test-IsGenerated {
    param([string] $Path)

    # Split on the real separators rather than pattern-matching a path. Regex with
    # escaped backslashes is easy to get subtly wrong, and a silently non-matching
    # filter would let generated files fail the build.
    $segments = $Path.Split(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)

    return ($segments -contains 'obj') -or ($segments -contains 'bin')
}

$offenders = @()

Get-ChildItem -Path $Root -Filter '*.razor' -Recurse -File |
    Where-Object { -not (Test-IsGenerated $_.FullName) } |
    ForEach-Object {
    $file = $_
    $content = Get-Content -LiteralPath $file.FullName -Raw

    if ([string]::IsNullOrWhiteSpace($content)) {
        return
    }

    # MudIconButton, across however many lines the tag is wrapped over.
    $matches = [regex]::Matches($content, '<MudIconButton\b[\s\S]*?/?>')

    foreach ($m in $matches) {
        $tag = $m.Value

        $hasLabel = $tag -match 'aria-label\s*=' -or
                    $tag -match '\bTitle\s*=' -or
                    $tag -match '\bAriaLabel\s*='

        if (-not $hasLabel) {
            $line = ($content.Substring(0, $m.Index) -split "`n").Count

            $offenders += [pscustomobject]@{
                File = $file.FullName
                Line = $line
                Rule = 'MudIconButton has no aria-label or Title. A screen reader announces nothing.'
                Text = ($tag -replace '\s+', ' ')
            }
        }
    }

    # aria-hidden needs a justification comment just above it. Look back a few
    # lines, not one - a justification worth writing rarely fits on a single line.
    $lines = $content -split "`r?`n"

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch 'aria-hidden\s*=\s*"true"') {
            continue
        }

        $from = [Math]::Max(0, $i - 4)
        $preceding = $lines[$from..([Math]::Max($from, $i - 1))] -join ' '

        if ($preceding -notmatch 'justification') {
            $offenders += [pscustomobject]@{
                File = $file.FullName
                Line = $i + 1
                Rule = 'aria-hidden without justification. Add an @* justification: ... *@ comment above it.'
                Text = $lines[$i].Trim()
            }
        }
    }
}

if ($offenders.Count -gt 0) {
    Write-Host ''
    Write-Host "Found $($offenders.Count) accessibility violation(s)." -ForegroundColor Red

    foreach ($o in $offenders) {
        Write-Host "##vso[task.logissue type=error;sourcepath=$($o.File);linenumber=$($o.Line);]$($o.Rule)"
        Write-Host "  $($o.File):$($o.Line)  $($o.Rule)"
        Write-Host "      $($o.Text)"
    }

    exit 1
}

Write-Host 'Gate 7 passed.' -ForegroundColor Green
exit 0

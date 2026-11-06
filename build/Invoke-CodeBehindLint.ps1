<#
********************************************************************************
Purpose : CI gate 6. Fails the build if any .razor file contains an @code block.
          All C# logic must live in the paired .razor.cs code-behind.
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

Write-Host 'Gate 6 - checking for @code blocks in .razor files...'


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
    $lineNumber = 0

    foreach ($line in (Get-Content -LiteralPath $file.FullName)) {
        $lineNumber++

        # Matches "@code {" and "@code{", with any leading whitespace.
        if ($line -match '^\s*@code\s*\{') {
            $offenders += [pscustomobject]@{
                File = $file.FullName
                Line = $lineNumber
                Text = $line.Trim()
            }
        }
    }
}

if ($offenders.Count -gt 0) {
    Write-Host ''
    Write-Host "Found $($offenders.Count) @code block(s). Move the logic to the .razor.cs code-behind." -ForegroundColor Red

    foreach ($o in $offenders) {
        Write-Host "##vso[task.logissue type=error;sourcepath=$($o.File);linenumber=$($o.Line);]@code block is not allowed. Use the paired .razor.cs file."
        Write-Host "  $($o.File):$($o.Line)  $($o.Text)"
    }

    exit 1
}

Write-Host 'Gate 6 passed - no @code blocks found.' -ForegroundColor Green
exit 0

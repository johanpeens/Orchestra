<#
********************************************************************************
Purpose : CI gate 5. Fails the build on raw HTML tables, non-MudBlazor dialogs,
          and ad-hoc hex colours outside the theme file.
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

Write-Host 'Gate 5 - MudBlazor-first lint...'

# BrandPalette.cs is the ONE place hex colours may be written down. It lives in
# the Application layer because these colours are data as well as styling -
# Department.ColourHex and StepStatus.ColourHex are seeded from it.
$colourAllowList = @(
    'BrandPalette.cs'
)


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

function Add-Offender {
    param($File, $Line, $Text, $Rule)

    $script:offenders += [pscustomobject]@{
        File = $File
        Line = $Line
        Text = $Text.Trim()
        Rule = $Rule
    }
}

Get-ChildItem -Path $Root -Include '*.razor', '*.css' -Recurse -File |
    Where-Object { -not (Test-IsGenerated $_.FullName) } |
    ForEach-Object {
    $file = $_
    $lineNumber = 0

    foreach ($line in (Get-Content -LiteralPath $file.FullName)) {
        $lineNumber++

        if ($file.Extension -eq '.razor') {
            # Raw HTML tables - use MudDataGrid.
            if ($line -match '<table[\s>]') {
                Add-Offender $file.FullName $lineNumber $line 'Raw <table>. Use MudDataGrid.'
            }

            # Hand-rolled modals - use MudDialog.
            if ($line -match 'class\s*=\s*"[^"]*\bmodal\b') {
                Add-Offender $file.FullName $lineNumber $line 'Hand-rolled modal. Use MudDialog.'
            }
        }

        # Ad-hoc hex colours. The theme file is the only exception.
        if ($colourAllowList -notcontains $file.Name -and $line -match '#[0-9A-Fa-f]{6}\b') {
            Add-Offender $file.FullName $lineNumber $line 'Hard-coded hex colour. Use the theme.'
        }
    }
}

# .cs files: hex colours only, and only outside the theme.
Get-ChildItem -Path $Root -Filter '*.cs' -Recurse -File |
    Where-Object { $colourAllowList -notcontains $_.Name -and -not (Test-IsGenerated $_.FullName) } |
    ForEach-Object {
        $file = $_
        $lineNumber = 0

        foreach ($line in (Get-Content -LiteralPath $file.FullName)) {
            $lineNumber++

            if ($line -match '"#[0-9A-Fa-f]{6}"') {
                Add-Offender $file.FullName $lineNumber $line 'Hard-coded hex colour. Use the theme.'
            }
        }
    }

if ($offenders.Count -gt 0) {
    Write-Host ''
    Write-Host "Found $($offenders.Count) MudBlazor-first violation(s)." -ForegroundColor Red

    foreach ($o in $offenders) {
        Write-Host "##vso[task.logissue type=error;sourcepath=$($o.File);linenumber=$($o.Line);]$($o.Rule)"
        Write-Host "  $($o.File):$($o.Line)  $($o.Rule)"
        Write-Host "      $($o.Text)"
    }

    exit 1
}

Write-Host 'Gate 5 passed.' -ForegroundColor Green
exit 0

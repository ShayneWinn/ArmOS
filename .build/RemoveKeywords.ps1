param (
    [string]$filePath
)

if (!(Test-Path $filePath)) {
    Write-Host "File not found: $filePath"
    exit 1
}

# List of keywords to remove
$keywords = @(
    '\bprivate\s?',     # Removes 'private' + optional space
    #'\bprotected\s?',   # Removes 'protected' + optional space
    '\breadonly\s?'     # Removes 'readonly' + optional space
)

# Read file content
$content = Get-Content $filePath -Raw

# Apply all keyword removals
foreach ($keyword in $keywords) {
    $content = $content -replace $keyword, ''
}

# Save back to the file
Set-Content -Path $filePath -Value $content

Write-Host "Minification complete: $filePath"

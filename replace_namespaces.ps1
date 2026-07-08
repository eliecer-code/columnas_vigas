$files = Get-ChildItem -Recurse -Include *.cs,*.xaml
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match 'CQEING_CuadroCantidades') {
        $content = $content -replace 'CQEING_CuadroCantidades', 'CompanyName.AddinName'
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$destDir = "D:\Ki6\Lap trinh web nang cao\project\FruitShop\docs\screenshots\extracted_doc"
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir | Out-Null
}
$zip = [System.IO.Compression.ZipFile]::OpenRead("D:\Ki6\Lap trinh web nang cao\project\FruitShop\BC_ATBMWEB\Bao_cao_bao_mat_website_ASPNET_check2.docx")
foreach ($entry in $zip.Entries) {
    if ($entry.FullName.StartsWith("word/media/")) {
        $filename = [System.IO.Path]::GetFileName($entry.FullName)
        $target = Join-Path $destDir $filename
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
    }
}
$zip.Dispose()
Write-Host "Extracted media files successfully."

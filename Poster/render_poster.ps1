# Re-render the A0 poster PDF + preview PNG from the HTML source.
# Run from anywhere:  powershell -File Poster\render_poster.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if (-not (Test-Path $edge)) { $edge = "C:\Program Files\Microsoft\Edge\Application\msedge.exe" }
$html = (Resolve-Path "$here\MissionFocus_Poster_A0.html").Path -replace '\\', '/'
& $edge --headless --disable-gpu --no-pdf-header-footer --virtual-time-budget=8000 `
    --print-to-pdf="$here\MissionFocus_Poster_A0.pdf" "file:///$html" | Out-Null
python -c "import fitz; d = fitz.open(r'$here\MissionFocus_Poster_A0.pdf'); p = d[0]; z = 3000/p.rect.width; p.get_pixmap(matrix=fitz.Matrix(z, z)).save(r'$here\MissionFocus_Poster_Preview.png'); print('PDF + preview rendered')"

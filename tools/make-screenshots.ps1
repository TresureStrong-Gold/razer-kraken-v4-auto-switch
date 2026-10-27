# Renders the tray menu (English and Korean) and the icon states into ../docs.
# Run with: powershell -NoProfile -STA -File tools\make-screenshots.ps1
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$out = Join-Path $PSScriptRoot '..\docs'
New-Item -ItemType Directory -Force $out | Out-Null

function Save-Menu($labels, $file) {
    $m = New-Object System.Windows.Forms.ContextMenuStrip
    $st = New-Object System.Windows.Forms.ToolStripMenuItem($labels[0]); $st.Enabled = $false
    [void]$m.Items.Add($st)
    [void]$m.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
    [void]$m.Items.Add($labels[1]); [void]$m.Items.Add($labels[2])
    [void]$m.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))
    $a = New-Object System.Windows.Forms.ToolStripMenuItem($labels[3]); $a.Checked = $true
    [void]$m.Items.Add($a); [void]$m.Items.Add($labels[4]); [void]$m.Items.Add($labels[5])
    $m.Show(300, 300)
    [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 400
    $bmp = New-Object System.Drawing.Bitmap($m.Width, $m.Height)
    $m.DrawToBitmap($bmp, (New-Object System.Drawing.Rectangle(0, 0, $m.Width, $m.Height)))
    $m.Close()
    $big = New-Object System.Drawing.Bitmap(($bmp.Width * 2), ($bmp.Height * 2))
    $g = [System.Drawing.Graphics]::FromImage($big); $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($bmp, 0, 0, $big.Width, $big.Height); $g.Dispose()
    $big.Save((Join-Path $out $file), [System.Drawing.Imaging.ImageFormat]::Png)
}

# Labels must match KrakenAutoSwitch.cs
Save-Menu @('Status: Headset on', 'Switch to Kraken now', 'Restore previous device now', 'Start with Windows', 'Open log', 'Exit') 'tray-menu.png'
Save-Menu @('상태: 헤드폰 켜짐', 'Kraken으로 지금 전환', '이전 장치로 지금 복원', 'Windows 시작 시 자동 실행', '로그 열기', '종료') 'tray-menu.ko.png'

# Icon states (same drawing as the app)
$ic = New-Object System.Drawing.Bitmap(540, 200)
$g = [System.Drawing.Graphics]::FromImage($ic)
$g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'; $g.Clear([System.Drawing.Color]::White)
$f = New-Object System.Drawing.Font('Segoe UI', 12)
$items = @(@('LimeGreen', 'Headset on'), @('Gray', 'Headset off'), @('Orange', 'Unknown (until first event)'))
$x = 20
foreach ($i in $items) {
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromName($i[0]))
    $g.FillEllipse($br, $x + 30, 30, 90, 90)
    $g.DrawEllipse([System.Drawing.Pens]::Black, $x + 30, 30, 90, 90)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'
    $g.DrawString($i[1], $f, [System.Drawing.Brushes]::Black, ([System.Drawing.RectangleF]::new([single]($x - 5), 135, 160, 50)), $sf)
    $x += 170
}
$g.Dispose()
$ic.Save((Join-Path $out 'tray-icons.png'), [System.Drawing.Imaging.ImageFormat]::Png)

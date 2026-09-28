$utf8 = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8
[Console]::OutputEncoding = $utf8

$message = [Console]::In.ReadToEnd()
$lines = $message -split "`r?`n"

foreach ($line in $lines) {
    if ($line -notmatch '^\s*Co-Authored-By:\s*Claude\b') {
        [Console]::Out.WriteLine($line)
    }
}

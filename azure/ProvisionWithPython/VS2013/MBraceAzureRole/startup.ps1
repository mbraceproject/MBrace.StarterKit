# Download Python installer
echo "Downloading python."
$url="https://www.python.org/ftp/python/2.7.9/python-2.7.9.msi"
Invoke-WebRequest $url -OutFile c:\python-2.7.9.msi

# Install Python
echo "Installing python."
msiexec /a c:\python-2.7.9.msi TARGETDIR=c:\Python27 ALLUSERS=1 ADDLOCAL=ALL /qn
$newPath = $env:Path + ";c:\Python27;c:\Python27\Scripts"
[Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")

# Sleep for a few seconds so the settings will have time to take effect.
sleep 10

# Export python path.
$env:Path = $env:Path + ";c:\Python27;c:\Python27\Scripts"

# Install pip
echo "Installing pip."
Invoke-WebRequest https://bootstrap.pypa.io/get-pip.py -OutFile c:\Python27\get-pip.py
python c:\Python27\get-pip.py

sleep 10
echo "Installing beautifulsoup4."
# Install beautifulsoup4 to parse web pages.
pip install beautifulsoup4

echo "Done."


# Zip up a Tiled2Unity install 
require 'colorize'
require 'FileUtils'
require 'rubygems'
require 'win32console'
require 'zip'

t2uVersion = ARGV.shift
if t2uVersion == nil
	puts "Need version string" 
	exit
end

# Check for version file
if !File.exists? "t2u-version.txt"
	puts "t2u-version.txt file not created" 
	exit
end

# Check for mathcing version
installedVersion = IO.read("t2u-version.txt")
if installedVersion != t2uVersion then
	puts "Error zipping Tiled2Unity installation".red
	puts "  Installed version is: #{installedVersion}"
	puts "  Version we asked for: #{t2uVersion}"
	exit
end

# Create the zip file for Tiled2Unity
zipName = "Tiled2Unity-#{t2uVersion}.zip"
puts "Creating zip file: #{zipName}"

FileUtils.rm(zipName) if File.exists? zipName

# Zip up all the files (but do not include the uninstaller)
dir = "C:/Program Files (x86)/Tiled2Unity/"
Zip::File.open(zipName, Zip::File::CREATE) do |zipfile|
	Dir[File.join(dir, '**', '**')].each do |file|
		next if File.basename(file) == "uninstall.exe"
		puts File.basename(file)
		zipfile.add(file.sub(dir, ''), file)
    end
end

# Create the zip fle for Tiled2UnityLite
zipName = "Tiled2UnityLite-#{t2uVersion}.zip"
puts "Creating zip file: #{zipName}"

FileUtils.rm(zipName) if File.exists? zipName
dir = "C:/Program Files (x86)/Tiled2Unity/"
Zip::File.open(zipName, Zip::File::CREATE) do |zipfile|
	# Copy .unitypackage and .cs files
	Dir[File.join(dir, '*.{unitypackage,cs}')].each do |file|
		puts File.basename(file)
		zipfile.add(file.sub(dir, ''), file)
    end
end



#!/usr/bin/env ruby
# This script looks for the right Tiled2Unity unitypackage file and packs it into the Tiled2UnityMac solution to be distributed
require 'nokogiri'
require 'FileUtils'

def get_version()
    # Open the Info.plist file to find out what version of Tiled2UnityMac we should be building
    plist = File.read("../Tiled2UnityMac/Info.plist")
    xml = Nokogiri::XML(plist)

    xml.css("key").each do |item|
        return item.next_element.text if item.text == "CFBundleShortVersionString"
    end
end


dir = File.dirname(__FILE__)
Dir.chdir(dir) do

    # Get the version of Tiled2UnityMac we are building
    version = get_version()

    # Look for the Unity package we want to embed in the build
    package = "Tiled2Unity.#{version}.unitypackage"

    puts "Error: Could not find package: #{package}. Select 'Tiled2Unity -> Export Tiled2Unity Library...' from Unity and export to this directory and try again." if not File.exist?(package)
    exit(1) if not File.exist?(package)

    puts "Copying package #{package} to Tiled2UnityMac solution (resources)"
    FileUtils.cp(package, "../Tiled2UnityMac/Resources/Tiled2Unity.unitypackage")
    
    # Write the version to disk so other parts of the build system can get it
    `echo #{version} > t2u_version.txt`
    
    exit(0)
end

require 'xcodeproj'
require 'fileutils'
$stdout.sync = true

begin
  project_path = ENV['PROJECT_FILE_PATH']
  build_directory = ENV['BUILT_PRODUCTS_DIR']
  main_app_target_name = ENV['TARGET_NAME']
  project_folder_path = ENV['PROJECT_FOLDER_PATH']
  project_bundle_id = ENV['BUNDLE_ID']
  PROVISIONING_PROFILE_SPECIFIER = ENV['PROVISIONING_PROFILE_SPECIFIER']

  puts "project_path:#{project_path}, project_folder_path:#{project_folder_path}"
  if project_path.to_s.empty? || build_directory.to_s.empty? || main_app_target_name.to_s.empty?
    puts "❌ Missing required environment variables."
    exit 1
  end

  project = Xcodeproj::Project.open(project_path)
  target_name = 'SkillzWidgetExtension'
  platform = :ios
  deployment_target = '16.0'
  plist_path = File.join(project_folder_path, 'SkillzWidgetExtension/Info.plist')


  if project.targets.any? { |t| t.name == target_name }
    puts "⚠️ Target '#{target_name}' already exists."
    exit 1
  end

  widget_target = project.new_target(:app_extension, target_name, platform, deployment_target)

  # Create proper product reference
  appex_ref = project.products_group.children.find do |child|
    child.path == "#{target_name}.appex"
  end

  unless appex_ref
    appex_ref = project.products_group.new_file("#{target_name}.appex")
    appex_ref.name = "#{target_name}.appex"
    appex_ref.path = "#{target_name}.appex"
    appex_ref.explicit_file_type = 'wrapper.app-extension'
    appex_ref.source_tree = 'BUILT_PRODUCTS_DIR'
  end
  widget_target.product_reference = appex_ref

  group = project.main_group[target_name] || project.main_group.new_group(target_name)

  main_app_target = project.targets.find { |t| t.name == main_app_target_name }

  widget_target.build_configurations.each do |config|
    main_config = main_app_target.build_configurations.find { |c| c.name == config.name }

    if main_config
      config.build_settings['DEVELOPMENT_TEAM'] = main_config.build_settings['DEVELOPMENT_TEAM']
      config.build_settings['CODE_SIGN_STYLE'] = main_config.build_settings['CODE_SIGN_STYLE'] || 'Manual'
      config.build_settings['CODE_SIGN_IDENTITY'] = main_config.build_settings['CODE_SIGN_IDENTITY']
      config.build_settings['CODE_SIGN_IDENTITY[sdk=iphoneos*]'] = main_config.build_settings['CODE_SIGN_IDENTITY[sdk=iphoneos*]']
      config.build_settings['PROVISIONING_PROFILE_SPECIFIER'] = PROVISIONING_PROFILE_SPECIFIER
    end

    config.build_settings['PRODUCT_BUNDLE_IDENTIFIER'] = "#{project_bundle_id}.#{target_name}"
    config.build_settings['TARGETED_DEVICE_FAMILY'] = '1,2'
    config.build_settings['INFOPLIST_FILE'] = plist_path
    config.build_settings['SWIFT_VERSION'] ||= '5.0'
    config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '16.2'
    config.build_settings['ASSETCATALOG_COMPILER_GLOBAL_ACCENT_COLOR_NAME'] = 'AccentColor'
    config.build_settings['ASSETCATALOG_COMPILER_WIDGET_BACKGROUND_COLOR_NAME'] = 'WidgetBackground'
    config.build_settings['CURRENT_PROJECT_VERSION'] = '1'
    config.build_settings['GENERATE_INFOPLIST_FILE'] = 'YES'
    config.build_settings['INFOPLIST_KEY_CFBundleDisplayName'] = 'WidgetExtension'
    config.build_settings['INFOPLIST_KEY_NSHumanReadableCopyright'] = ''
    config.build_settings['LD_RUNPATH_SEARCH_PATHS'] = [
      '$(inherited)',
      '@executable_path/Frameworks',
      '@executable_path/../../Frameworks'
    ]
    config.build_settings['MARKETING_VERSION'] = '1.0'
    config.build_settings['PRODUCT_NAME'] = '$(TARGET_NAME)'
    config.build_settings['SKIP_INSTALL'] = 'YES'
    config.build_settings['SWIFT_EMIT_LOC_STRINGS'] = 'YES'
  end

  plist_content = <<~XML
    <?xml version="1.0" encoding="UTF-8"?>
    <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
    <plist version="1.0">
    <dict>
      <key>NSExtension</key>
      <dict>
        <key>NSExtensionPointIdentifier</key>
        <string>com.apple.widgetkit-extension</string>
      </dict>
    </dict>
    </plist>
  XML

  FileUtils.mkdir_p(File.dirname(plist_path))
  File.write(plist_path, plist_content)
  plist_ref = group.new_file(plist_path)
  group.children.delete(plist_ref)
  group.children << plist_ref


  widget_target.add_system_framework('WidgetKit')
  widget_target.add_system_framework('SwiftUI')

  source_dir = File.join(build_directory, 'SKZLiveActivity')
  destination_dir = File.join(project_folder_path, 'SkillzWidgetExtension')
  FileUtils.mkdir_p(destination_dir)
  FileUtils.cp_r("#{source_dir}/.", destination_dir)

  assets_added = false

  Dir.glob("#{destination_dir}/**/*").each do |file|
    # Skip Info.plist
    next if File.basename(file) == "Info.plist"
    next if File.basename(file).end_with?('.meta')

    # Handle Assets.xcassets once
    if !assets_added && File.directory?(file) && File.basename(file) == "Assets.xcassets"
      file_ref = group.new_file(file)
      widget_target.resources_build_phase.add_file_reference(file_ref)
      assets_added = true
      next
    end

    # Skip contents of Assets.xcassets
    next if file.include?("Assets.xcassets/")

    unless File.directory?(file)
          file_ref = group.new_file(file)
          widget_target.add_file_references([file_ref])
    end


  end
  # --- Add App Group Capability to the extension target ---
entitlements_path = File.join(project_folder_path, 'SkillzWidgetExtension/SkillzWidgetExtension.entitlements')
app_group_id = "group.#{project_bundle_id}"

# Create entitlements plist
entitlements_content = <<~XML
  <?xml version="1.0" encoding="UTF-8"?>
  <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
  <plist version="1.0">
  <dict>
    <key>com.apple.security.application-groups</key>
    <array>
      <string>#{app_group_id}</string>
    </array>
  </dict>
  </plist>
XML

File.write(entitlements_path, entitlements_content)

# Add entitlements file to the extension target build settings
widget_target.build_configurations.each do |config|
  config.build_settings['CODE_SIGN_ENTITLEMENTS'] = entitlements_path
end

# Add the entitlements file to the Xcode project group
entitlements_ref = group.new_file(entitlements_path)
group.children.delete(entitlements_ref)
group.children << entitlements_ref

  # Embed the .appex in main app
  unless main_app_target.nil?
    embed_phase = main_app_target.copy_files_build_phases.find { |p| p.name == 'Embed Foundation Extensions' }
    unless embed_phase
      embed_phase = project.new(Xcodeproj::Project::Object::PBXCopyFilesBuildPhase)
      embed_phase.name = 'Embed Foundation Extensions'
      embed_phase.dst_subfolder_spec = '13'
      embed_phase.build_action_mask = '2147483647'
      embed_phase.run_only_for_deployment_postprocessing = '0'
      main_app_target.build_phases << embed_phase
    end
    appex_build_file = embed_phase.files.find { |f| f.file_ref == appex_ref }
    unless appex_build_file
       # If the reference is not already added, add it and set attributes
       appex_build_file = embed_phase.add_file_reference(appex_ref)
     end

     # Set the attributes for the appex build file
     appex_build_file.settings = { 'ATTRIBUTES' => ['RemoveHeadersOnCopy'] }
    
  else
    puts "⚠️ Main app target '#{main_app_target_name}' not found."
  end

  # Add target dependency and proxy
  unless main_app_target.dependencies.any? { |d| d.target == widget_target }
    proxy = project.new(Xcodeproj::Project::Object::PBXContainerItemProxy)
    proxy.container_portal = project.root_object.uuid
    proxy.proxy_type = '1'
    proxy.remote_global_id_string = widget_target.uuid
    proxy.remote_info = widget_target.name

    dependency = project.new(Xcodeproj::Project::Object::PBXTargetDependency)
    dependency.target = widget_target
    dependency.target_proxy = proxy

    main_app_target.dependencies << dependency
  end
  
  unity_framework_target = project.targets.find { |t| t.name == 'UnityFramework' } # Change if your target has a different name

  if unity_framework_target
    widget_files = Dir.glob("#{destination_dir}/**/*.swift").map { |f| File.expand_path(f) }

    unity_framework_target.source_build_phase.files.each do |build_file|
      next unless build_file.file_ref
      file_path = File.expand_path(build_file.file_ref.real_path.to_s) rescue nil
      next unless file_path

      if widget_files.include?(file_path)
        puts "Removing #{file_path} from Unity framework target"
        unity_framework_target.source_build_phase.remove_build_file(build_file)
      end
    end
  end


  project.save
  puts "✅ Widget extension '#{target_name}' added, linked, and embedded successfully."
  exit 0
rescue StandardError => e
  puts "❌ Error occurred: #{e}"
  puts e.backtrace.join("\n")
  exit 1
end

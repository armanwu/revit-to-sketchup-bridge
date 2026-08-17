require 'sketchup.rb'
require 'extensions.rb'

module RevitImporter
  unless defined?(RevitImporter::EXTENSION_LOADED)
    EXTENSION_LOADED = true

    ext = SketchupExtension.new(
      'Revit JSON Importer',
      File.join(File.dirname(__FILE__), 'revit_importer', 'revit_importer.rb')
    )
    ext.description = "Imports geometry exported from the Revit 'Export to SketchUp' add-in (.json), grouped by Revit category and level, with per-face colors and safe cylindrical surface reconstruction."
    ext.version = '1.0.0'
    ext.creator = 'Arman Arisman'
    Sketchup.register_extension(ext, true)
  end
end

using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace RevitToSketchUpExporter
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel panel = application.CreateRibbonPanel("SketchUp Bridge");

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "ExportToSketchUpBtn",
                "Export RVT\nto JSON",
                thisAssemblyPath,
                "RevitToSketchUpExporter.ExportCommand")
            {
                ToolTip = "Export the selected elements (or all elements in the active 3D view) to a JSON file " +
                          "that can be read by the 'revit_importer.rb' Ruby script in SketchUp."
            };

            Assembly assembly = Assembly.GetExecutingAssembly();

            // Load icons with Pack URI first (Autodesk standard), with MemoryStream fallback
            buttonData.LargeImage = LoadIcon(assembly, "icon128.png", "RevitToSketchUpExporter.Resources.icon128.png");
            buttonData.Image = LoadIcon(assembly, "icon64.png", "RevitToSketchUpExporter.Resources.icon64.png");

            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static BitmapImage LoadIcon(Assembly assembly, string resourceFileName, string manifestResourceName)
        {
            // Try WPF Pack URI
            try
            {
                Uri packUri = new Uri($"pack://application:,,,/{assembly.GetName().Name};component/Resources/{resourceFileName}", UriKind.Absolute);
                BitmapImage packImg = new BitmapImage(packUri);
                if (packImg != null) return packImg;
            }
            catch
            {
                // Ignore and try stream fallback
            }

            // Fallback: Read Assembly Manifest Stream into MemoryStream
            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(manifestResourceName))
                {
                    if (stream == null) return null;
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);

                    MemoryStream ms = new MemoryStream(buffer);
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}

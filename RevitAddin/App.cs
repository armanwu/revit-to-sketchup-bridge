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

            // Load 32x32 icon for LargeImage
            using (Stream streamLarge = assembly.GetManifestResourceStream("RevitToSketchUpExporter.Resources.icon32.png"))
            {
                if (streamLarge != null)
                {
                    buttonData.LargeImage = BitmapFrame.Create(streamLarge);
                }
            }

            // Load 16x16 icon for small Image
            using (Stream streamSmall = assembly.GetManifestResourceStream("RevitToSketchUpExporter.Resources.icon16.png"))
            {
                if (streamSmall != null)
                {
                    buttonData.Image = BitmapFrame.Create(streamSmall);
                }
            }

            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}

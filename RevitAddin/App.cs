using Autodesk.Revit.UI;
using System.Reflection;

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
                "Export to SketchUp (JSON)",
                thisAssemblyPath,
                "RevitToSketchUpExporter.ExportCommand")
            {
                ToolTip = "Export the selected elements (or all elements in the active 3D view) to a JSON file " +
                          "that can be read by the 'revit_importer.rb' Ruby script in SketchUp."
            };

            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}

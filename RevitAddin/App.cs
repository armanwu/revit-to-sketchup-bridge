using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitToSketchUpExporter
{
    /// <summary>
    /// Revit entry point. Adds a ribbon tab + button that runs ExportCommand.
    /// </summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            const string tabName = "SketchUp Bridge";

            try { application.CreateRibbonTab(tabName); }
            catch { /* tab already exists, ignore */ }

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Export");

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "ExportToSketchUpBtn",
                "Export to\nSketchUp (JSON)",
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

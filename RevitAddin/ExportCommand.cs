using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RevitToSketchUpExporter
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportCommand : IExternalCommand
    {
        // Revit's internal unit is feet. The SketchUp Ruby API treats plain
        // numeric Point3d coordinates as inches, so everything is multiplied by 12.
        private const double FeetToInches = 12.0;

        // Geometric tolerances are expressed in the units noted by each constant.
        private const double LengthToleranceFeet = 1e-9;
        private const double SolidVolumeToleranceCuFt = 1e-9;
        private const double ParameterTolerance = 1e-9;
        private const double DirectionTolerance = 1e-8;
        private const double CylinderAreaRelativeTolerance = 1e-5;
        private const double CylinderAreaAbsoluteToleranceSqFt = 1e-8;
        private const double TriangleCrossProductToleranceSqIn = 1e-6;

        private static readonly int[] DefaultColor = { 200, 200, 200 };

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            ICollection<ElementId> selectedIds =
                uidoc.Selection.GetElementIds();

            List<Element> targetElements;

            if (selectedIds.Count > 0)
            {
                targetElements = selectedIds
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null && e.Category != null)
                    .ToList();
            }
            else
            {
                targetElements =
                    new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .WhereElementIsNotElementType()
                        .Where(e =>
                            e.Category != null &&
                            e.Category.HasMaterialQuantities)
                        .ToList();
            }

            if (targetElements.Count == 0)
            {
                TaskDialog.Show(
                    "Export to SketchUp",
                    "No exportable elements found. Select some elements first, " +
                    "or open a 3D view that contains the model.");

                return Result.Cancelled;
            }

            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "SketchUp Exchange JSON (*.json)|*.json",
                FileName =
                    SanitizeFileName(doc.Title) +
                    "_sketchup_export.json"
            };

            if (dlg.ShowDialog() != DialogResult.OK)
                return Result.Cancelled;

            Options geomOptions = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            var exportedElements = new List<ExportedElement>();
            int skipped = 0;

            foreach (Element el in targetElements)
            {
                try
                {
                    GeometryElement geomElem =
                        el.get_Geometry(geomOptions);

                    if (geomElem == null)
                    {
                        skipped++;
                        continue;
                    }

                    // Fallback color in case a face has no material of its own.
                    int[] elementFallbackColor =
                        GetElementColor(doc, el);

                    var faces = new List<ExportedFace>();

                    CollectFaces(
                        geomElem,
                        doc,
                        Transform.Identity,
                        elementFallbackColor,
                        faces);

                    if (faces.Count == 0)
                    {
                        skipped++;
                        continue;
                    }

                    exportedElements.Add(
                        new ExportedElement
                        {
                            Id = GetElementIdValue(el.Id),
                            Category =
                                el.Category?.Name ??
                                "Uncategorized",
                            Name = el.Name ?? "",
                            LevelName =
                                GetLevelName(doc, el),
                            Faces = faces
                        });
                }
                catch
                {
                    skipped++;
                }
            }

            if (exportedElements.Count == 0)
            {
                TaskDialog.Show(
                    "Export to SketchUp",
                    "No solid geometry could be exported.");

                return Result.Failed;
            }

            string json =
                JsonWriter.Serialize(
                    exportedElements,
                    doc.Title);

            File.WriteAllText(
                dlg.FileName,
                json,
                new UTF8Encoding(false));

            TaskDialog.Show(
                "Export to SketchUp",
                $"Successfully exported " +
                $"{exportedElements.Count} element(s) to:\n" +
                $"{dlg.FileName}" +
                (skipped > 0
                    ? $"\n\n{skipped} element(s) skipped " +
                      "(no solid geometry)."
                    : "") +
                "\n\nNext step: open SketchUp, click Extensions > " +
                "Import Revit Export (JSON)..., then select this file.");

            return Result.Succeeded;
        }

        private void CollectFaces(
            GeometryElement geomElem,
            Document doc,
            Transform transform,
            int[] fallbackColor,
            List<ExportedFace> faces)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid &&
                    solid.Faces.Size > 0 &&
                    solid.Volume > SolidVolumeToleranceCuFt)
                {
                    foreach (Face face in solid.Faces)
                    {
                        int[] faceColor =
                            GetFaceColor(
                                doc,
                                face,
                                fallbackColor);

                        // Export only simple, untrimmed cylindrical patches
                        // as analytic cylinders. Any cylindrical face that
                        // cannot safely be represented as one rectangular
                        // UV patch falls back to triangulation.
                        if (face is CylindricalFace cylFace &&
                            TryBuildCylindricalFace(
                                cylFace,
                                transform,
                                faceColor,
                                out ExportedFace curvedFace))
                        {
                            faces.Add(curvedFace);
                        }
                        else
                        {
                            AddTriangulatedFace(
                                face,
                                transform,
                                faceColor,
                                faces);
                        }
                    }
                }
                else if (obj is GeometryInstance instance)
                {
                    // GetInstanceGeometry() already returns a transformed
                    // copy of the instance geometry. Applying
                    // instance.Transform again would apply the placement
                    // twice.
                    GeometryElement instGeom =
                        instance.GetInstanceGeometry();

                    if (instGeom != null)
                    {
                        CollectFaces(
                            instGeom,
                            doc,
                            transform,
                            fallbackColor,
                            faces);
                    }
                }
                else if (obj is Mesh mesh)
                {
                    int[] meshColor =
                        GetMeshColor(
                            doc,
                            mesh,
                            fallbackColor);

                    AddTriangulatedMesh(
                        mesh,
                        transform,
                        meshColor,
                        faces);
                }
            }
        }

        // Builds an analytic description only when the cylindrical
        // face is a simple rectangular patch in UV space.
        // Trimmed/opened/irregular faces are deliberately rejected
        // and exported through Face.Triangulate().
        private bool TryBuildCylindricalFace(
            CylindricalFace cylFace,
            Transform transform,
            int[] color,
            out ExportedFace result)
        {
            result = null;

            try
            {
                // Split-face regions are safer to preserve through
                // triangulation than as one analytic patch.
                if (cylFace.HasRegions)
                    return false;

                BoundingBoxUV bbox =
                    cylFace.GetBoundingBox();

                if (bbox == null)
                    return false;

                double uMin = bbox.Min.U;
                double uMax = bbox.Max.U;
                double vMin = bbox.Min.V;
                double vMax = bbox.Max.V;

                double angleSpan =
                    uMax - uMin;

                double height =
                    vMax - vMin;

                if (angleSpan <= ParameterTolerance ||
                    height <= LengthToleranceFeet)
                {
                    return false;
                }

                // A cylindrical face should not require more
                // than one complete turn.
                if (angleSpan >
                    (2.0 * Math.PI) + 1e-6)
                {
                    return false;
                }

                // CylindricalFace.Radius is an XYZ radius vector,
                // not a scalar radius.
                XYZ radiusVector =
                    cylFace.get_Radius(0);

                if (radiusVector == null)
                    return false;

                double radius =
                    radiusVector.GetLength();

                if (radius <= LengthToleranceFeet)
                    return false;

                // For a simple rectangular cylindrical UV patch:
                //
                // area = radius * angle * height
                //
                // If the actual face area is different, the cylinder
                // contains trimming, openings, slanted boundaries, etc.
                // In that situation reconstructing the whole bounding
                // rectangle would create geometry that does not exist.
                double expectedArea =
                    radius *
                    angleSpan *
                    height;

                double areaTolerance =
                    Math.Max(
                        CylinderAreaAbsoluteToleranceSqFt,
                        expectedArea *
                        CylinderAreaRelativeTolerance);

                if (Math.Abs(
                        cylFace.Area -
                        expectedArea) >
                    areaTolerance)
                {
                    return false;
                }

                XYZ axisLocal =
                    cylFace.Axis;

                if (axisLocal == null ||
                    axisLocal.GetLength() <=
                    LengthToleranceFeet)
                {
                    return false;
                }

                axisLocal =
                    axisLocal.Normalize();

                // Point on the cylinder axis at v = vMin.
                XYZ originLocal =
                    cylFace.Origin.Add(
                        axisLocal.Multiply(vMin));

                UV startUv =
                    new UV(
                        uMin,
                        vMin);

                XYZ p0Local =
                    cylFace.Evaluate(startUv);

                if (p0Local == null)
                    return false;

                XYZ radialLocal =
                    p0Local.Subtract(originLocal);

                double evaluatedRadius =
                    radialLocal.GetLength();

                if (evaluatedRadius <=
                    LengthToleranceFeet)
                {
                    return false;
                }

                // Make sure the radius obtained through Evaluate()
                // agrees with CylindricalFace.Radius.
                double radiusTolerance =
                    Math.Max(
                        LengthToleranceFeet,
                        radius * 1e-6);

                if (Math.Abs(
                        evaluatedRadius -
                        radius) >
                    radiusTolerance)
                {
                    return false;
                }

                XYZ startRefDirLocal =
                    radialLocal.Normalize();

                // BasisX is the tangent in the positive U direction.
                // It lets us determine whether the Revit cylinder
                // parameterization follows the right-hand rule around
                // the cylinder Axis.
                Transform derivatives =
                    cylFace.ComputeDerivatives(startUv);

                if (derivatives == null ||
                    derivatives.BasisX == null ||
                    derivatives.BasisX.GetLength() <=
                    LengthToleranceFeet)
                {
                    return false;
                }

                XYZ tangentULocal =
                    derivatives.BasisX.Normalize();

                XYZ originWorld =
                    transform.OfPoint(originLocal);

                XYZ axisWorld =
                    transform
                        .OfVector(axisLocal)
                        .Normalize();

                XYZ startRefDirWorld =
                    transform
                        .OfVector(startRefDirLocal)
                        .Normalize();

                XYZ tangentUWorld =
                    transform
                        .OfVector(tangentULocal)
                        .Normalize();

                double handedness =
                    axisWorld
                        .CrossProduct(
                            startRefDirWorld)
                        .DotProduct(
                            tangentUWorld);

                if (Math.Abs(handedness) <
                    DirectionTolerance)
                {
                    return false;
                }

                XYZ refDirWorld;

                if (handedness >= 0.0)
                {
                    // Normal right-handed sweep.
                    refDirWorld =
                        startRefDirWorld;
                }
                else
                {
                    // Revit's parameterization is reversed relative
                    // to SketchUp's positive right-hand sweep.
                    //
                    // Instead of writing a negative AngleSpan, use the
                    // other end of the Revit U range as the starting
                    // radial direction. This preserves a positive
                    // AngleSpan and keeps the JSON format compatible
                    // with the existing importer.
                    UV endUv =
                        new UV(
                            uMax,
                            vMin);

                    XYZ p1Local =
                        cylFace.Evaluate(endUv);

                    if (p1Local == null)
                        return false;

                    XYZ endRadialLocal =
                        p1Local.Subtract(
                            originLocal);

                    double endRadius =
                        endRadialLocal.GetLength();

                    if (endRadius <=
                            LengthToleranceFeet ||
                        Math.Abs(
                            endRadius -
                            radius) >
                            radiusTolerance)
                    {
                        return false;
                    }

                    refDirWorld =
                        transform
                            .OfVector(
                                endRadialLocal
                                    .Normalize())
                            .Normalize();
                }

                result =
                    new ExportedFace
                    {
                        Type = "cylinder",

                        Origin = new[]
                        {
                            originWorld.X *
                                FeetToInches,

                            originWorld.Y *
                                FeetToInches,

                            originWorld.Z *
                                FeetToInches
                        },

                        Axis = new[]
                        {
                            axisWorld.X,
                            axisWorld.Y,
                            axisWorld.Z
                        },

                        RefDir = new[]
                        {
                            refDirWorld.X,
                            refDirWorld.Y,
                            refDirWorld.Z
                        },

                        Radius =
                            radius *
                            FeetToInches,

                        AngleSpan =
                            angleSpan,

                        Height =
                            height *
                            FeetToInches,

                        Color =
                            color
                    };

                return true;
            }
            catch
            {
                // If analytic reconstruction fails for any reason,
                // the caller will fall back to triangulation.
                return false;
            }
        }

        private void AddTriangulatedFace(
            Face face,
            Transform transform,
            int[] color,
            List<ExportedFace> faces)
        {
            Mesh mesh =
                face.Triangulate();

            if (mesh == null)
                return;

            AddTriangulatedMesh(
                mesh,
                transform,
                color,
                faces);
        }

        private void AddTriangulatedMesh(
            Mesh mesh,
            Transform transform,
            int[] color,
            List<ExportedFace> faces)
        {
            for (int i = 0;
                 i < mesh.NumTriangles;
                 i++)
            {
                MeshTriangle tri =
                    mesh.get_Triangle(i);

                var verts =
                    new List<double[]>(3);

                for (int v = 0;
                     v < 3;
                     v++)
                {
                    XYZ p =
                        transform.OfPoint(
                            tri.get_Vertex(v));

                    verts.Add(
                        new[]
                        {
                            p.X *
                                FeetToInches,

                            p.Y *
                                FeetToInches,

                            p.Z *
                                FeetToInches
                        });
                }

                if (IsDegenerate(verts))
                    continue;

                faces.Add(
                    new ExportedFace
                    {
                        Type = "mesh",
                        Vertices = verts,
                        Color = color
                    });
            }
        }

        // A triangle is degenerate when the cross product
        // of two edges is effectively zero.
        private bool IsDegenerate(
            List<double[]> v)
        {
            if (v == null ||
                v.Count != 3)
            {
                return true;
            }

            double ax =
                v[1][0] -
                v[0][0];

            double ay =
                v[1][1] -
                v[0][1];

            double az =
                v[1][2] -
                v[0][2];

            double bx =
                v[2][0] -
                v[0][0];

            double by =
                v[2][1] -
                v[0][1];

            double bz =
                v[2][2] -
                v[0][2];

            double cx =
                (ay * bz) -
                (az * by);

            double cy =
                (az * bx) -
                (ax * bz);

            double cz =
                (ax * by) -
                (ay * bx);

            double crossLengthSquared =
                (cx * cx) +
                (cy * cy) +
                (cz * cz);

            double toleranceSquared =
                TriangleCrossProductToleranceSqIn *
                TriangleCrossProductToleranceSqIn;

            return crossLengthSquared <=
                   toleranceSquared;
        }

        private string GetLevelName(
            Document doc,
            Element el)
        {
            try
            {
                ElementId levelId =
                    el.LevelId;

                if (levelId != null &&
                    levelId !=
                    ElementId.InvalidElementId)
                {
                    if (doc.GetElement(levelId)
                        is Level level)
                    {
                        return level.Name;
                    }
                }
            }
            catch
            {
                // Some elements do not expose a usable LevelId.
            }

            return "";
        }

        // Prefer the material assigned directly to the face.
        private int[] GetFaceColor(
            Document doc,
            Face face,
            int[] fallback)
        {
            try
            {
                ElementId matId =
                    face.MaterialElementId;

                if (matId != null &&
                    matId !=
                    ElementId.InvalidElementId)
                {
                    if (doc.GetElement(matId)
                            is Material mat &&
                        mat.Color.IsValid)
                    {
                        return new[]
                        {
                            (int)mat.Color.Red,
                            (int)mat.Color.Green,
                            (int)mat.Color.Blue
                        };
                    }
                }
            }
            catch
            {
                // Use fallback.
            }

            return fallback;
        }

        private int[] GetMeshColor(
            Document doc,
            Mesh mesh,
            int[] fallback)
        {
            try
            {
                ElementId matId =
                    mesh.MaterialElementId;

                if (matId != null &&
                    matId !=
                    ElementId.InvalidElementId)
                {
                    if (doc.GetElement(matId)
                            is Material mat &&
                        mat.Color.IsValid)
                    {
                        return new[]
                        {
                            (int)mat.Color.Red,
                            (int)mat.Color.Green,
                            (int)mat.Color.Blue
                        };
                    }
                }
            }
            catch
            {
                // Use fallback.
            }

            return fallback;
        }

        // Last-resort material fallback before default gray.
        private int[] GetElementColor(
            Document doc,
            Element el)
        {
            try
            {
                ElementId matId =
                    el.GetMaterialIds(false)
                        .FirstOrDefault();

                if (matId != null &&
                    matId !=
                    ElementId.InvalidElementId)
                {
                    if (doc.GetElement(matId)
                            is Material mat &&
                        mat.Color.IsValid)
                    {
                        return new[]
                        {
                            (int)mat.Color.Red,
                            (int)mat.Color.Green,
                            (int)mat.Color.Blue
                        };
                    }
                }
            }
            catch
            {
                // Use default color.
            }

            return DefaultColor;
        }

        // Revit 2024+ uses the 64-bit ElementId.Value.
        // Older Revit API generations expose IntegerValue.
        // Reflection makes this source usable with both forms.
        private long GetElementIdValue(
            ElementId id)
        {
            if (id == null)
                return -1;

            var valueProperty =
                typeof(ElementId)
                    .GetProperty("Value");

            if (valueProperty != null)
            {
                return Convert.ToInt64(
                    valueProperty.GetValue(
                        id,
                        null));
            }

            var integerValueProperty =
                typeof(ElementId)
                    .GetProperty(
                        "IntegerValue");

            if (integerValueProperty != null)
            {
                return Convert.ToInt64(
                    integerValueProperty.GetValue(
                        id,
                        null));
            }

            throw new InvalidOperationException(
                "Unable to read ElementId value.");
        }

        private string SanitizeFileName(
            string name)
        {
            foreach (char c in
                     Path.GetInvalidFileNameChars())
            {
                name =
                    name.Replace(
                        c,
                        '_');
            }

            return name;
        }
    }

    internal class ExportedElement
    {
        public long Id;
        public string Category;
        public string Name;
        public string LevelName;
        public List<ExportedFace> Faces;
    }

    internal class ExportedFace
    {
        // "mesh" or "cylinder".
        public string Type = "mesh";

        // Type == "mesh"
        public List<double[]> Vertices;

        // Type == "cylinder"
        public double[] Origin;
        public double[] Axis;
        public double[] RefDir;
        public double Radius;
        public double AngleSpan;
        public double Height;

        public int[] Color;
    }
}
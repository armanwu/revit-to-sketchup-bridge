using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RevitToSketchUpExporter
{
    /// <summary>
    /// Small, dependency-free JSON serializer so the add-in doesn't need an
    /// extra NuGet package (Newtonsoft.Json etc.) that could clash with
    /// Revit's own assembly versions.
    /// </summary>
    internal static class JsonWriter
    {
        public static string Serialize(List<ExportedElement> elements, string sourceDocTitle)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"source\": ").Append(Quote(sourceDocTitle)).Append(",\n");
            sb.Append("  \"units\": \"inches\",\n");
            sb.Append("  \"elements\": [\n");

            for (int i = 0; i < elements.Count; i++)
            {
                ExportedElement e = elements[i];
                sb.Append("    {\n");
                sb.Append("      \"id\": ").Append(e.Id).Append(",\n");
                sb.Append("      \"category\": ").Append(Quote(e.Category)).Append(",\n");
                sb.Append("      \"name\": ").Append(Quote(e.Name)).Append(",\n");
                sb.Append("      \"level\": ").Append(Quote(e.LevelName)).Append(",\n");
                sb.Append("      \"faces\": [\n");

                for (int f = 0; f < e.Faces.Count; f++)
                {
                    ExportedFace face = e.Faces[f];
                    sb.Append("        { \"type\": ").Append(Quote(face.Type)).Append(", ");

                    if (face.Type == "cylinder")
                    {
                        sb.Append("\"origin\": [")
                          .Append(Num(face.Origin[0])).Append(", ")
                          .Append(Num(face.Origin[1])).Append(", ")
                          .Append(Num(face.Origin[2])).Append("], ");
                        sb.Append("\"axis\": [")
                          .Append(Num(face.Axis[0])).Append(", ")
                          .Append(Num(face.Axis[1])).Append(", ")
                          .Append(Num(face.Axis[2])).Append("], ");
                        sb.Append("\"refDir\": [")
                          .Append(Num(face.RefDir[0])).Append(", ")
                          .Append(Num(face.RefDir[1])).Append(", ")
                          .Append(Num(face.RefDir[2])).Append("], ");
                        sb.Append("\"radius\": ").Append(Num(face.Radius)).Append(", ");
                        sb.Append("\"angleSpan\": ").Append(Num(face.AngleSpan)).Append(", ");
                        sb.Append("\"height\": ").Append(Num(face.Height));
                    }
                    else
                    {
                        sb.Append("\"v\": [");
                        for (int v = 0; v < face.Vertices.Count; v++)
                        {
                            double[] p = face.Vertices[v];
                            sb.Append("[")
                              .Append(Num(p[0])).Append(", ")
                              .Append(Num(p[1])).Append(", ")
                              .Append(Num(p[2]))
                              .Append("]");
                            if (v < face.Vertices.Count - 1) sb.Append(", ");
                        }
                        sb.Append("]");
                    }

                    sb.Append(", \"c\": [")
                      .Append(face.Color[0]).Append(", ").Append(face.Color[1]).Append(", ").Append(face.Color[2])
                      .Append("] }");
                    if (f < e.Faces.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }

                sb.Append("      ]\n");
                sb.Append("    }");
                if (i < elements.Count - 1) sb.Append(",");
                sb.Append("\n");
            }

            sb.Append("  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string Num(double d)
        {
            return d.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static string Quote(string s)
        {
            s ??= "";
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}

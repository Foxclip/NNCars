#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Xml.Linq;
using SyntaxTree.VisualStudio.Unity.Bridge;
using UnityEditor;

/// <summary>
/// Provide a hook into Unity's Project File Generation so that StyleCop gets re-added each time.
/// </summary>
[InitializeOnLoad]
public class ProjectFileHook
{
    private const string StypCopVersionStr = "1.1.118";

    static ProjectFileHook()
    {
        ProjectFilesGenerator.ProjectFileGeneration += (string name, string content) =>
        {
            // parse the document and make some changes
            var document = XDocument.Parse(content);
            XNamespace xmlns = "http://schemas.microsoft.com/developer/msbuild/2003";
            XElement itemGroup = new XElement(xmlns + "ItemGroup");
            itemGroup.Add(new XElement(xmlns + "Analyzer", new XAttribute("Include", "packages\\StyleCop.Analyzers." + StypCopVersionStr + "\\analyzers\\dotnet\\cs\\StyleCop.Analyzers.CodeFixes.dll")));
            itemGroup.Add(new XElement(xmlns + "Analyzer", new XAttribute("Include", "packages\\StyleCop.Analyzers." + StypCopVersionStr + "\\analyzers\\dotnet\\cs\\StyleCop.Analyzers.dll")));
            document.Root.Add(itemGroup);
            document.Root.Add(new XElement(xmlns + "ItemGroup", new XElement(xmlns + "AdditionalFiles", new XAttribute("Include", "stylecop.json"))));
            document.Root.Add(new XElement(xmlns + "ItemGroup", new XElement(xmlns + "Compile", new XAttribute("Include", "GlobalSuppressions.cs"))));

            // save the changes using the Utf8StringWriter
            var str = new Utf8StringWriter();
            document.Save(str);

            return str.ToString();
        };
    }

    // necessary for XLinq to save the xml project file in utf8
    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
#endif
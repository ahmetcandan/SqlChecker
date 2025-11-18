using System.Xml;

namespace MsSqlAnalyze;

public static class ExecutionPlanAnalize
{
    public static IEnumerable<AnalysisResult> AnalyzeExecutionPlan(string planXml)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(planXml);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("sql", "http://schemas.microsoft.com/sqlserver/2004/07/showplan");

            var scanOperations = GetScanFinding(xmlDoc, namespaceManager)
                .Union(GetWarningFinding(xmlDoc, namespaceManager));

            return scanOperations.Any()
                ? scanOperations
                : [new("Execution Plan", AnalysisStatus.Successfull, "Execution Plan was reviewed. No findings were identified.")];
        }
        catch
        {
            return [new("Execution Plan", AnalysisStatus.Error, "Error: XML parser !")];
        }
    }

    private static IEnumerable<AnalysisResult> GetScanFinding(XmlDocument xml, XmlNamespaceManager namespaceManager)
    {
        string xPath = "//sql:RelOp[@PhysicalOp=\"Clustered Index Scan\"] | //sql:RelOp[@PhysicalOp=\"Index Scan\"] | //sql:RelOp[@PhysicalOp=\"Table Scan\"]";
        var scanNodes = xml.SelectNodes(xPath, namespaceManager);

        if (scanNodes?.Count > 0)
            foreach (XmlNode node in scanNodes)
            {
                if (node.Attributes?["PhysicalOp"]?.Value.Equals("Index Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:IndexScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        yield return new("Execution Plan", AnalysisStatus.Warning, $"Index Scan (NonClustered): {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}, Indeks: {objInfo.IndexName}");
                }
                else if (node.Attributes?["PhysicalOp"]?.Value.Equals("Clustered Index Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:IndexScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        yield return new("Execution Plan", AnalysisStatus.Warning, $"Clustered Index Scan: {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}, Indeks: {objInfo.IndexName}");
                }
                else if (node.Attributes?["PhysicalOp"]?.Value.Equals("Table Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:TableScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        yield return new("Execution Plan", AnalysisStatus.Warning, $"Table Scan: {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}");
                }
            }
    }

    private static IEnumerable<AnalysisResult> GetWarningFinding(XmlDocument xml, XmlNamespaceManager namespaceManager)
    {
        var warningNode = xml.SelectSingleNode("//sql:Warnings", namespaceManager);

        if (warningNode is not null)
            foreach (XmlNode warning in warningNode.ChildNodes)
                yield return new(warning.Name, AnalysisStatus.Warning, $"{(warning.Attributes?.Count > 0 ? warning.Attributes[0].Value : string.Empty)} {(warning.Attributes?.Count > 1 ? warning.Attributes[1].Value : string.Empty)} {(warning.Attributes?.Count > 2 ? warning.Attributes[2].Value : string.Empty)}");
    }

    private static ObjectInfo? GetObjectInfoFromNode(XmlNode? node)
    {
        return node?.Attributes is null
            ? null
            : new()
            {
                TableAlias = node.Attributes?["Alias"]?.Value,
                TableName = node.Attributes?["Table"]?.Value,
                SchemeName = node.Attributes?["Schema"]?.Value,
                IndexName = node.Attributes?["Index"]?.Value
            };
    }

    private class ObjectInfo
    {
        public string? TableAlias { get; set; }
        public string? TableName { get; set; }
        public string? SchemeName { get; set; }
        public string? IndexName { get; set; }
    }
}

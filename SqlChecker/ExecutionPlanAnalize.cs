using System.Xml;

namespace SqlChecker;

public static class ExecutionPlanAnalize
{
    public static List<AnalysisResult> AnalyzeExecutionPlan(string planXml)
    {
        List<AnalysisResult> scanOperations = [];

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(planXml);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("sql", "http://schemas.microsoft.com/sqlserver/2004/07/showplan");
            string xPath = "//sql:RelOp[@PhysicalOp=\"Clustered Index Scan\"] | //sql:RelOp[@PhysicalOp=\"Index Scan\"] | //sql:RelOp[@PhysicalOp=\"Table Scan\"]";

            var scanNodes = xmlDoc.SelectNodes(xPath, namespaceManager);
            if (scanNodes == null || scanNodes.Count == 0)
            {
                scanOperations.Add(new("Execution Plan", AnalysisStatus.Error, "Not found Execution Plan."));
                return scanOperations;
            }

            foreach (XmlNode node in scanNodes)
            {
                if (node.Attributes?["PhysicalOp"]?.Value.Equals("Index Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:IndexScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        scanOperations.Add(new("Execution Plan", AnalysisStatus.Warning, $"Index Scan (NonClustered): {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}, Indeks: {objInfo.IndexName}"));
                }
                else if (node.Attributes?["PhysicalOp"]?.Value.Equals("Clustered Index Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:IndexScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        scanOperations.Add(new("Execution Plan", AnalysisStatus.Warning, $"Clustered Index Scan: {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}, Indeks: {objInfo.IndexName}"));
                }
                else if (node.Attributes?["PhysicalOp"]?.Value.Equals("Table Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:TableScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        scanOperations.Add(new("Execution Plan", AnalysisStatus.Warning, $"Table Scan: {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}"));
                }
            }

            var warningNode = xmlDoc.SelectSingleNode("//sql:Warnings", namespaceManager);
            if (warningNode is not null)
                foreach (XmlNode warning in warningNode.ChildNodes)
             		{
																string at0 = warning.Attributes?[0]?.Value ?? string.Empty;
string at1 = warning.Attributes?[1]?.Value ?? string.Empty;
string at2 = warning.Attributes?[2]?.Value ?? string.Empty;
      scanOperations.Add(new(warning.Name, AnalysisStatus.Warning, $"{at0} {at1} {at2}"));
														} 
        }
        catch
        {
            scanOperations.Add(new("Execution Plan", AnalysisStatus.Error, $"Error: XML parser !"));
        }

        return scanOperations;
    }

    private static ObjectInfo? GetObjectInfoFromNode(XmlNode? node)
    {
        if (node?.Attributes is null)
            return null;

        return new()
        {
            TableAlias = node.Attributes?["Alias"]?.Value,
            TableName = node.Attributes?["Table"]?.Value,
            SchemeName = node.Attributes?["Schema"]?.Value,
            IndexName = node.Attributes?["Index"]?.Value
        };
    }

    class ObjectInfo
    {
        public string? TableAlias { get; set; }
        public string? TableName { get; set; }
        public string? SchemeName { get; set; }
        public string? IndexName { get; set; }
    }
}

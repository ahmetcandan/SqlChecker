using System.Xml;

namespace SqlChecker;

public static class ExecutionPlanAnalize
{
    public static List<string> AnalyzeExecutionPlan(string planXml)
    {
        List<string> scanOperations = [];

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
                scanOperations.Add("Not found Execution Plan.");
                return scanOperations;
            }

            foreach (XmlNode node in scanNodes)
            {
                if (node.Attributes?["PhysicalOp"]?.Value.Equals("Index Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:IndexScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        scanOperations.Add($"Index Scan (NonClustered): {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}, Indeks: {objInfo.IndexName}");
                }
                else if (node.Attributes?["PhysicalOp"]?.Value.Equals("Clustered Index Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:IndexScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        scanOperations.Add($"Clustered Index Scan: {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}, Indeks: {objInfo.IndexName}");
                }
                else if (node.Attributes?["PhysicalOp"]?.Value.Equals("Table Scan", StringComparison.OrdinalIgnoreCase) is true)
                {
                    var objInfo = GetObjectInfoFromNode(node.SelectSingleNode("sql:TableScan", namespaceManager)?.SelectSingleNode("sql:Object", namespaceManager));
                    if (objInfo is not null)
                        scanOperations.Add($"Table Scan: {(!string.IsNullOrEmpty(objInfo.SchemeName) ? $"{objInfo.SchemeName}." : "")}{objInfo.TableName} {(!string.IsNullOrEmpty(objInfo.TableAlias) ? $"({objInfo.TableAlias})" : string.Empty)}");
                }
            }
        }
        catch (Exception ex)
        {
            scanOperations.Add($"Error: XML parser. {ex.Message}");
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

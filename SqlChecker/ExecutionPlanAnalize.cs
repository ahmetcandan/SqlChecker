using System.Xml;

namespace SqlChecker;

public static class ExecutionPlanAnalize
{
    public static List<string> AnalyzeExecutionPlan(string planXml)
    {
        var scanOperations = new List<string>();

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(planXml);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("sql", "http://schemas.microsoft.com/sqlserver/2004/07/showplan");
            string xPath = "//sql:RelOp/sql:IndexScan | //sql:RelOp/sql:TableScan";

            var scanNodes = xmlDoc.SelectNodes(xPath, namespaceManager);

            if (scanNodes == null || scanNodes.Count == 0)
            {
                scanOperations.Add("Not found Execution Plan.");
                return scanOperations;
            }

            foreach (XmlNode node in scanNodes)
            {
                if (node.SelectSingleNode("sql:SeekPredicates", namespaceManager) is not null)
                    continue;

                string operationType = node.LocalName;
                var objectNode = node.SelectSingleNode("sql:Object", namespaceManager);

                string tableAlias = "Unknown";
                string tableName = "Unknown";
                string schemeName = "";
                string indexName = "-";

                if (objectNode is not null)
                {
                    tableAlias = objectNode.Attributes["Alias"]?.Value ?? tableAlias;
                    tableName = objectNode.Attributes["Table"]?.Value ?? tableName;
                    schemeName = objectNode.Attributes["Schema"]?.Value ?? string.Empty;
                    indexName = objectNode.Attributes["Index"]?.Value ?? indexName;
                    if (operationType == "IndexScan")
                    {
                        if (string.IsNullOrEmpty(indexName))
                            indexName = "Clustered Index Scan (PK)";
                    }
                    else if (operationType == "TableScan")
                        indexName = "No Index (Heap Table Scan)";

                    string result = $"{operationType}: {(!string.IsNullOrEmpty(schemeName) ? $"{schemeName}." : "")}{tableName} ({tableAlias}), Indeks: {indexName}";
                    scanOperations.Add(result);
                }
            }
        }
        catch (Exception ex)
        {
            scanOperations.Add($"Error: XML parser. {ex.Message}");
        }

        return scanOperations;
    }
}

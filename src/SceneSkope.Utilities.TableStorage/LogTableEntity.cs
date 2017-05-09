using Microsoft.WindowsAzure.Storage.Table;

namespace SceneSkope.Utilities.TableStorage
{
    public class LogTableEntity : TableEntity
    {
        public static string FormatRowKey(int lineNumber) => $"{lineNumber:D10}";
        public static int ParseRoadKey(string rowKey) => int.Parse(rowKey);

        public LogTableEntity()
        {
        }

        public LogTableEntity(string fileName, int lineNumber)
        {
            PartitionKey = fileName;
            RowKey = FormatRowKey(lineNumber);
        }

        public byte[] Data { get; set; }
    }
}

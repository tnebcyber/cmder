namespace FormCMS.Utils.EventStreaming;

public record RecordMessage(string Operation, string EntityName,  string Id, Record Data)
{
    public string Key => $"{EntityName}_{Id}";
}
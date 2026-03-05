using System.Text.Json;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace HybridOutbox.DynamoDb.Internals;

internal sealed class DictionaryObjectConverter : IPropertyConverter
{
    public DynamoDBEntry ToEntry(object value)
    {
        if (value is not Dictionary<string, object> dict || dict.Count == 0)
            return new Primitive(string.Empty);

        return new Primitive(JsonSerializer.Serialize(dict));
    }

    public object FromEntry(DynamoDBEntry entry)
    {
        var json = entry.AsString();
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, object>();

        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
               ?? new Dictionary<string, object>();
    }
}

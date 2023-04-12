using System.Reflection;
using System.Text.RegularExpressions;

public abstract class BaseConfig
{
    public void Render(Dictionary<string, string> properties)
    {
        foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Public))
        {
            foreach (var prop in properties)
            {
                var val = property.GetValue(this);
                var query = @"{{\s*" + prop.Key + @"\s*}}";

                if (val is null)
                {
                    continue;
                }

                if (val is string && Regex.IsMatch((string)val, query))
                {
                    property.SetValue(this, Regex.Replace((string)val, query, prop.Value.ToString()));
                }

                if (val is List<string> && ((List<string>)val).Count > 0)
                {
                    var list = (List<string>)val;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (Regex.IsMatch(list[i], query))
                        {
                            list[i] = Regex.Replace(list[i], query, prop.Value.ToString());
                        }
                    }
                }

                if (val is Dictionary<string, string> && ((Dictionary<string, string>)val).Count > 0)
                {
                    var dict = (Dictionary<string, string>)val;
                    foreach (var key in dict.Keys)
                    {
                        if (Regex.IsMatch(dict[key], query))
                        {
                            dict[key] = Regex.Replace(dict[key], query, prop.Value.ToString());
                        }
                    }
                }
            }
        }
    }
}
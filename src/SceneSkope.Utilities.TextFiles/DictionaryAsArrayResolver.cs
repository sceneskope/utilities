using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Linq;
using System.Collections;

namespace SceneSkope.Utilities.TextFiles
{
    public class DictionaryAsArrayResolver : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            if (objectType.GetTypeInfo().ImplementedInterfaces.Any(i => i == typeof(IDictionary) ||
                (i.GetTypeInfo().IsGenericType &&
                 i.GetGenericTypeDefinition() == typeof(IDictionary<,>))))
            {
                return base.CreateArrayContract(objectType);
            }

            return base.CreateContract(objectType);
        }

    }
}

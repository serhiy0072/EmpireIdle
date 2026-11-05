using Microsoft.Extensions.Configuration.Json;

namespace EmpireIdle.API.Configuration
{
    /// <summary>
    /// JSON-провайдер із готовим індексом дочірніх ключів. Стандартний на кожен вузол перебирає всі ключі файлу,
    /// і на сотнях кілобайтів конфіга гри прив'язка GameConfig займала секунди — двічі за кожен старт.
    /// Читання й ключі ті самі, змінюється лише спосіб знайти дітей вузла.
    /// </summary>
    public sealed class IndexedJsonConfigurationProvider : JsonConfigurationProvider
    {
        private Dictionary<string, HashSet<string>>? _children;

        public IndexedJsonConfigurationProvider(JsonConfigurationSource source) : base(source) { }

        public override void Load(Stream stream)
        {
            base.Load(stream);
            _children = null;
        }

        /// <summary>Значення можуть дописати збоку (ConfigurationRoot роздає Set усім провайдерам).</summary>
        public override void Set(string key, string? value)
        {
            base.Set(key, value);
            _children = null;
        }

        public override IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
        {
            _children ??= BuildIndex();

            var results = new List<string>(earlierKeys);

            if (_children.TryGetValue(parentPath ?? string.Empty, out var children))
                results.AddRange(children);

            // Той самий порядок, що в базового: від нього залежать індекси списків при прив'язці
            results.Sort(ConfigurationKeyComparer.Instance);

            return results;
        }

        private Dictionary<string, HashSet<string>> BuildIndex()
        {
            var index = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in Data.Keys)
            {
                var start = 0;

                while (true)
                {
                    var delimiter = key.IndexOf(ConfigurationPath.KeyDelimiter, start, StringComparison.Ordinal);
                    var parent = start == 0 ? string.Empty : key[..(start - ConfigurationPath.KeyDelimiter.Length)];
                    var segment = delimiter < 0 ? key[start..] : key[start..delimiter];

                    if (!index.TryGetValue(parent, out var children))
                        index[parent] = children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    children.Add(segment);

                    if (delimiter < 0)
                        break;

                    start = delimiter + ConfigurationPath.KeyDelimiter.Length;
                }
            }

            return index;
        }
    }

    public sealed class IndexedJsonConfigurationSource : JsonConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);

            return new IndexedJsonConfigurationProvider(this);
        }
    }

    public static class IndexedJsonConfigurationExtensions
    {
        /// <summary>Обов'язковий файл конфіга гри: без перезавантаження на зміну, як і решта.</summary>
        public static IConfigurationBuilder AddIndexedJsonFile(this IConfigurationBuilder builder, string path)
            => builder.Add<IndexedJsonConfigurationSource>(source =>
            {
                source.Path = path;
                source.Optional = false;
                source.ReloadOnChange = false;
                source.ResolveFileProvider();
            });
    }
}

using System;
using System.IO;
using System.Text.Json;

namespace Trading.Strategies.Regimes
{
    /// <summary>
    /// Serializa / deserializa <see cref="PersistedHmmModel"/> a JSON indentado.
    ///
    /// Política de formato: JSON legible en code review (WriteIndented = true). El modelo
    /// se commitea al repo como artefacto versionado; revisarlo en un PR debe ser viable.
    /// </summary>
    public static class HmmModelSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            IncludeFields = false
        };

        public static void Save(PersistedHmmModel model, string path)
        {
            if (model is null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path no puede estar vacío.", nameof(path));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(model, SerializerOptions);
            File.WriteAllText(path, json);
        }

        public static PersistedHmmModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path no puede estar vacío.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"No se encontró el modelo HMM serializado en '{path}'. " +
                    "Ejecutá el HmmTrainer para generarlo o verificá el path.",
                    path);

            string json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<PersistedHmmModel>(json, SerializerOptions);
            if (model is null)
                throw new InvalidDataException(
                    $"El archivo '{path}' deserializó a null. El JSON está corrupto o vacío.");
            return model;
        }
    }
}

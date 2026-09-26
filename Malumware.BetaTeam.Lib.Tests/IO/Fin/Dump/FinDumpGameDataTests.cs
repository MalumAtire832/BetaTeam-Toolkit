using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Dump;
using Malumware.BetaTeam.Lib.Tests.GameData;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Dump
{
    public class FinDumpGameDataTests
    {
        [GameDataFact]
        public void Build_ReturnsObjectsWithUniqueFields_ForEveryShippedFile()
        {
            // Arrange
            var duplicates = new List<string>();
            var count = 0;

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                count++;
                var dump = FinDumpBuilder.Build(FinReader.Read(name, data));
                foreach (var root in dump.Roots.Concat(dump.Unreferenced))
                {
                    CollectDuplicates(name, root, duplicates);
                }
            }

            // Assert
            Assert.NotEqual(0, count);
            Assert.Empty(duplicates.Distinct());
        }

        [GameDataFact]
        public void Write_ProducesTextAndValidJson_ForEveryShippedFile()
        {
            // Arrange
            var count = 0;

            // Act & Assert
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                count++;
                var dump = FinDumpBuilder.Build(FinReader.Read(name, data));

                var text = new StringWriter();
                FinTextDumpWriter.Write(dump, text, full: true);
                Assert.StartsWith($"{name} (FIN version 23)", text.ToString());

                var json = new MemoryStream();
                FinJsonDumpWriter.Write(dump, json);
                // Deep scene trees nest past System.Text.Json's default reading depth of 64
                var options = new System.Text.Json.JsonDocumentOptions { MaxDepth = 1000 };
                using (var document = System.Text.Json.JsonDocument.Parse(json.ToArray(), options))
                {
                    Assert.Equal(name, document.RootElement.GetProperty("name").GetString());
                }
            }
            Assert.NotEqual(0, count);
        }

        private static void CollectDuplicates(string file, FinDumpNode node, List<string> duplicates)
        {
            switch (node)
            {
                case FinDumpObject obj:
                    var repeated = obj.Children
                        .GroupBy(c => c.Field)
                        .Where(g => g.Count() > 1);
                    foreach (var group in repeated)
                    {
                        duplicates.Add($"{obj.ClassName}.{group.Key}");
                    }
                    foreach (var child in obj.Children)
                    {
                        CollectDuplicates(file, child, duplicates);
                    }
                    break;
                case FinDumpList list:
                    foreach (var item in list.Items)
                    {
                        CollectDuplicates(file, item, duplicates);
                    }
                    break;
            }
        }
    }
}

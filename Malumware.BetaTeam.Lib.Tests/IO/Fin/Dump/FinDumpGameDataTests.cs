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

        private static void CollectDuplicates(string file, FinDumpNode node, List<string> duplicates)
        {
            switch (node)
            {
                case FinDumpObject obj:
                    foreach (var group in obj.Children.GroupBy(c => c.Field).Where(g => g.Count() > 1))
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

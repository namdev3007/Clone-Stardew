using Plugins.Lowscope.ComponentSaveSystem.Enums;

namespace Plugins.Lowscope.ComponentSaveSystem.Data
{
    public interface IConvertSaveGame
    {
        SaveGame ConvertTo(StorageType storageType, string filePath, bool replace = true);
    }
}

namespace Referencing.Scriptable_Assets
{
    public interface IReferenceableAsset
    {
        string GetGuid();

        // Required when checking for duplicate guids.
        void GenerateNewGuid();
    }
}

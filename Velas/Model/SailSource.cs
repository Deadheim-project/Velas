namespace Velas.Model
{
    /// <summary>Where a sail's definition came from. Generic sails ship inside the mod and
    /// always work offline; Remote sails are discovered from the manifest at
    /// <see cref="SailConfig.SailsRepositoryUrl"/> and require a successful download (or a
    /// cache hit) before they can be applied.</summary>
    public enum SailSource
    {
        Generic,
        Remote,
    }
}

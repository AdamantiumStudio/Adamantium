using Adamantium.Fonts.Common;

namespace Adamantium.Fonts.Tables.Layout
{
    public abstract class LookupSubTableBase : ILookupSubTable
    {
        public abstract FeatureKind OwnerType { get; }
    }
}

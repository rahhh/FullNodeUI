using System.Text;

namespace Xels.Bitcoin.Interfaces
{
    public interface IFeatureStats
    {
        void AddFeatureStats(StringBuilder benchLog);
    }
}

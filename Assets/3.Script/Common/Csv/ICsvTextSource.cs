using System.Threading;
using System.Threading.Tasks;

namespace EscapeGame.Data.Csv
{
    public interface ICsvTextSource
    {
        Task<string> ReadTextAsync(string sourceName, CancellationToken token);
    }
}

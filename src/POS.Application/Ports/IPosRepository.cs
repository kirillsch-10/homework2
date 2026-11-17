using POS.Application.Contracts;

namespace POS.Application.Ports;

public interface IPosRepository
{
    Task<PosSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PosSnapshot snapshot, CancellationToken cancellationToken = default);
}

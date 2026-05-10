using LicenceBackend.Core.Users;

namespace LicenceBackend.Core.Sessions;

public interface ISessionTokenIssuer
{
    SessionToken Issue(User user, Guid sessionId);
}

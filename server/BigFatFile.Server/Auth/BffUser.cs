using Microsoft.AspNetCore.Identity;

namespace BigFatFile.Server.Auth;

internal sealed class BffUser : IdentityUser<Guid>
{
    public BffUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }
}

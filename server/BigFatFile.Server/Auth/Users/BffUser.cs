using Microsoft.AspNetCore.Identity;

namespace BigFatFile.Server.Auth.Users;

internal sealed class BffUser : IdentityUser<Guid>
{
    public BffUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }
}

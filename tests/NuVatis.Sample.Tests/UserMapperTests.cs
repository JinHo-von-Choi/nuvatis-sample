using NuVatis.Sample.Core.Mappers;
using NuVatis.Sample.Core.Models;

namespace NuVatis.Sample.Tests;

public class UserMapperTests
{
    private readonly IUserMapper _userMapper;

    public UserMapperTests()
    {
        // TODO: DI setup
        _userMapper = null!;
    }

    [Fact]
    public void GetAll_ShouldReturnUsers()
    {
        // Arrange
        
        // Act
        var users = _userMapper.GetAll();

        // Assert
        Assert.NotNull(users);
    }
}

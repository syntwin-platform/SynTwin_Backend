using Syntwin.Application.Auth.Dtos;
using Syntwin.Domain.Entities;

namespace Syntwin.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    GeneratedJwtToken Generate(User user);
}
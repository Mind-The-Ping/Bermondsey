using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bermondsey.Models;

public enum PhoneOS
{
    IOS = 0,
    Android = 1,
}

public record UserDetails(
    Guid Id, 
    string PhoneNumber, 
    PhoneOS PhoneOS);

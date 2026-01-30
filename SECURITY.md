# Security Policy

## Reporting Security Vulnerabilities

**Do not** open public issues for security vulnerabilities. Instead:

1. **Email security@mcp-tool-shop.dev** (or maintainers directly if that address is unavailable)
2. **Include**:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if you have one)

3. **What to expect**:
   - Acknowledgment within 48 hours
   - Updates on progress every week
   - Credit in security advisory (if desired)

## Supported Versions

| Version | Supported          | Status          |
|---------|------------------|-----------------|
| 1.x     | ✅ Yes            | Current release |
| 0.x     | 🚫 No             | Beta only       |

Security updates are provided for the latest version. We recommend always using the latest version.

## Security Considerations

### Local-First Architecture

Control Room is designed as a local-first application:

- All data stored in **SQLite** on your local machine
- **No cloud sync** - Data never leaves your computer
- **No authentication required** - Assumes trusted environment
- **Network access optional** - Can run completely offline

### Storage Security

- **SQLite with WAL mode** for safe concurrent access
- **No encryption** - Rely on OS-level file permissions
- **Log files** may contain sensitive data (scripts, arguments)

### Recommended Practices

1. **Protect your database** - Restrict file access via OS permissions
2. **Review script contents** - Only execute trusted scripts
3. **Monitor log files** - Archive/delete old logs containing sensitive data
4. **Keep .NET updated** - Security patches are released regularly

## Known Limitations

### Not Designed For

- **Multi-user systems** - No built-in user isolation
- **Sensitive credentials** - Scripts should use OS-level secrets management
- **Untrusted environments** - Assume all code/data is trusted
- **Remote access** - No secure remote execution features

### Recommendations

1. Use **environment variables** for secrets, not hardcoded values
2. Use **OS credential managers** (Windows Credential Manager, etc.)
3. **Audit scripts** before execution
4. **Limit database access** via file system permissions
5. **Backup database** regularly

## Security Updates

- **Critical vulnerabilities** (RCE, data loss): Patch within 24 hours
- **High vulnerabilities** (DoS, information leak): Patch within 1 week
- **Medium/Low**: Included in next regular release

## Dependency Management

Control Room minimizes dependencies:

- **MAUI** - Microsoft's official framework
- **CommunityToolkit.MVVM** - Official community toolkit
- **SQLite** - Bundled with .NET

All dependencies:
- ✅ Monitored for vulnerabilities
- ✅ Updated regularly
- ✅ Audited before release

## Responsible Disclosure

We appreciate responsible disclosure! Please:

1. ✅ **DO** - Report privately and give us time to respond
2. ✅ **DO** - Be specific about the vulnerability
3. ✅ **DO** - Include reproduction steps
4. 🚫 **DON'T** - Create public issues
5. 🚫 **DON'T** - Exploit vulnerabilities
6. 🚫 **DON'T** - Share details before we release a fix

## Security Advisories

Once fixed, vulnerabilities are published as:
- GitHub Security Advisories
- Release notes with "SECURITY" tag
- Detailed technical explanation

## Questions?

Contact the maintainers with security questions or concerns.

# Security Policy

## 🔒 Security Considerations

This is a **public repository** containing Unity game source code. Please follow these security guidelines:

### **What's Safe to Commit:**
- ✅ Game source code
- ✅ Configuration templates with placeholder values
- ✅ Documentation and setup instructions
- ✅ Example files with dummy data
- ✅ Unity project files and assets

### **What's NEVER Safe to Commit:**
- ❌ API keys or secrets
- ❌ Database passwords
- ❌ Real CDN URLs with credentials
- ❌ Personal information
- ❌ Payment processing keys
- ❌ Third-party service credentials

## 🚨 Reporting Security Issues

If you discover a security vulnerability in this repository:

1. **DO NOT** create a public issue
2. **DO NOT** discuss it in public forums
3. **DO** contact the maintainer privately
4. **DO** provide detailed information about the issue

## 🛡️ Development Security

### **Environment Variables**
Use environment variables for sensitive configuration:

```csharp
// ✅ GOOD
string cdnUrl = Environment.GetEnvironmentVariable("CDN_URL") 
                ?? "https://your-cdn-domain.com/game-config/";

// ❌ BAD
string cdnUrl = "https://my-real-cdn.aws.com/game-config/";
```

### **Configuration Files**
Always use placeholder values in committed config files:

```json
{
  "cdnUrl": "https://your-cdn-domain.com/game-config/",
  "apiEndpoint": "https://your-api-domain.com/api/",
  "timeout": 10
}
```

### **CDN Setup**
The CDN configuration uses placeholder URLs. Replace with your actual CDN URL:

1. Set `CDN_URL` environment variable
2. Or update `CDNSetup` component in Unity
3. Or use `CDNConfigUI` for runtime configuration

## 🔍 Security Checklist

Before contributing:

- [ ] No hardcoded secrets in code
- [ ] No real URLs with credentials
- [ ] No personal information
- [ ] No API keys or tokens
- [ ] Configuration files use placeholders
- [ ] .gitignore properly configured
- [ ] No sensitive data in commit history

## 📋 Setup Instructions

### **For Developers:**
1. Clone the repository
2. Set up environment variables for your CDN
3. Configure CDN settings in Unity
4. Test with your actual CDN URL

### **For Contributors:**
1. Use placeholder values in all configs
2. Document any new configuration options
3. Update .gitignore if needed
4. Follow the security checklist

## 🚨 Emergency Response

If sensitive data is accidentally committed:

1. **Immediately** remove the data
2. **Force push** to remove from history
3. **Rotate** any exposed credentials
4. **Notify** affected services
5. **Review** all recent commits

## 📞 Contact

For security-related questions or issues, please contact the repository maintainer privately.

---

**Remember: Once data is public on GitHub, consider it compromised!**


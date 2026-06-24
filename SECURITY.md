# �� SECURITY.md

## �� Secure Code Review Summary

This document summarizes the findings from a secure code review and automated tools assessment of this application.

---

## �� Threat Modeling Notes

Link to Confluence page: https://ca-il-confluence.il.cyber-ark.com/spaces/rndp/pages/694873538/Final+Exercise+-+G14

---

## Security Findings

All Snyk findings (Open Source / SCA, Code / SAST, and Container) are published to this
repository's **Security tab → Code scanning alerts**:

https://github.com/cybr-champions-summary/g14-insecure-mongo-app/security/code-scanning

The scans run automatically on every push to `main`, so the alerts always reflect the current
state of the code — when you fix and merge an issue, its alert is resolved on the next run.

---

## ⚠️ Key Vulnerabilities Identified

###1. [Sensitive Data Exposure / Broken Authentication]
- **Location** - 73b2a2e0950474fd95ff8e9871aa5c8304e6467e:.github/workflows/ci.yml:custom-mongodb-uri:81
- **Issue** - password to MongoDB revealed
- **Risk** - unauthorized access to DB as admin
- **Recommendation** - store password in more secure way, (preferably GitHub secret but no permission)



###2. [Sensitive Data Exposure / Broken Authentication]
- **Location** - File: InsecureMongoApp.API/Handlers/InsecureHandler.cs Line: 20
- **Location** - File: InsecureMongoApp.API/Handlers/UserService.cs Line: 16
- **Issue** - The issue is a hardcoded connection string with plaintext credentials
- **Risk** - Credential Exposure, Lateral Movement, Persistence in Git History
- **Recommendation** - Store connection URL in env variable secured/encrypted with DPAPI on the machine

###3 Cryptography is not secure enough [SYNK HIGH]
- **Location** - File:  InsecureMongoApp.API/Handlers/InsecureHandler.cs, line 75
- **Issue** -  RSA is configured with an insecure 1024-bit key size.
- **Risk** -It can be feasibly broken, leading to data decryption or impersonation.
- **Recommendation** - Use at least a 2048-bit RSA key (preferably 3072+ for stronger security).
...

###3 A hardcoded string is used to encrypt data within [SYNK HIGH]
- **Location** - File:  InsecureMongoApp.API/Handlers/AuthHandler.cs, line 31
- **Location** - File: InsecureMongoApp.API/Handlers/AuthHandler.cs, line 58
- **Issue** -  Token hash is not protected with know secret
- **Risk** - attacter can prepare his own token
- **Recommendation** - store the secret in a secure way, for example in env variable secured/encrypted with DPAPI on the machine .
...

Fill it as you feel for the most high priority vulnerabilities you find

---

## ✅ Suggested Secure Practices

- [ ] Input validation implemented
- [ ] Secrets are not hardcoded
- [ ] Database access follows least privilege principle
- [ ] Rate limiting or request throttling applied

---

> �� Please complete all sections based on your findings. This document is required even if you do not compile or run the application.

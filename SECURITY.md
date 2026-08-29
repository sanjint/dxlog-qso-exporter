# Security Policy

## Supported Versions

Only the latest released version of DXLog QSO Exporter is actively supported with security fixes.

| Version | Supported          |
| ------- | ------------------ |
| Latest  | :white_check_mark: |
| < 1.0.0 | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability or privacy issue in DXLog QSO Exporter:

1. **Do NOT open a public GitHub issue** to report a security vulnerability.
2. Please report the issue privately using GitHub's **Private Vulnerability Reporting** feature on the repository (under the **Security** tab -> **Report a vulnerability**).
3. If private reporting is unavailable, contact the project maintainers directly.

Please include:
- A description of the issue and potential impact.
- Steps or a minimal reproducible example to recreate the issue.
- Details of your operating system and .NET runtime environment.

We will review reports promptly, acknowledge receipt, and coordinate a fix and advisory release.

## Privacy Guidelines for Bug Reports and Logs

- **Never upload raw contest database files (`.dxn`) or personal audio recordings** that contain personal information, passwords, or station credentials.
- When submitting logs or export reports, use synthetic sample data or ensure that callsigns, operator names, and sensitive details have been redacted.
- DXLog QSO Exporter generated reports automatically redact local file paths and Windows usernames to help protect your privacy.


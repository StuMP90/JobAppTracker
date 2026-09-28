# Privacy Policy for JobAppTracker

**Effective Date:** September 28, 2026  
**Application:** JobAppTracker  
**Developer:** StuMP90  

---

## 1. Overview
JobAppTracker is a standalone Windows desktop application designed to help individuals track their job search, applications, notes, and recruitment interactions. We believe in total data ownership and digital privacy.

## 2. Information Collection and Storage
- **100% Local Storage:** All data entered into JobAppTracker (including job titles, company names, contact details, notes, interview history, salaries, and attached documents) is stored exclusively on your local computer in a local SQLite database (`%LOCALAPPDATA%\JobAppTracker\`).
- **No External Servers:** JobAppTracker does not operate any remote servers, cloud databases, or backends. Your data is never uploaded, transferred, or synced to any external server or third party.
- **No Telemetry or Tracking:** The application does not contain any analytics SDKs, telemetry, crash reporting trackers, advertising libraries, or user behavior tracking mechanisms.

## 3. Network and Internet Access
JobAppTracker operates entirely offline. Network access is only triggered when you explicitly perform the following user-initiated actions:
- Clicking a job advertisement link or company website URL saved in an application record, which opens the link in your default web browser.

## 4. Document Attachments
When you attach a file (such as a CV, job specification, or PDF) to an application record, the file is copied to a private local directory on your device (`%LOCALAPPDATA%\JobAppTracker\Attachments\`). These files remain strictly on your machine and are only opened when you explicitly select "Open File" or "Open Folder".

## 5. Data Retention and Deletion
Because all data resides strictly on your local machine, you have complete control over it at all times:
- You can edit, delete, or export your records directly within the application.
- You can delete your database and all attachments at any time by opening the data folder in Settings and deleting the files.
- Uninstalling the application removes the program binaries from your system.

## 6. Open Source Transparency
JobAppTracker is open source. You can inspect the complete source code, database schemas, and build configurations at:  
https://github.com/StuMP90/JobAppTracker

## 7. Contact
If you have any questions or feedback regarding this privacy policy, you may open an issue on GitHub:  
https://github.com/StuMP90/JobAppTracker/issues

# Quick Deployment Guide

## Deploy the Fix in 5 Minutes

### Step 1: Stop the MCP Server

```bash
sudo systemctl stop delphi-analysis-mcp
```

### Step 2: Backup Current Version

```bash
cd /path/to/DelphiAnalysisMcpServer
cd ..
cp -r DelphiAnalysisMcpServer DelphiAnalysisMcpServer.backup.$(date +%Y%m%d_%H%M%S)
```

### Step 3: Deploy Fixed Files

Extract the zip and copy the three changed files:

```bash
# Option A: Replace specific files
cp DelphiAnalysisMcpServer_FIXED/Models/DatabaseEntities.cs \
   /path/to/DelphiAnalysisMcpServer/Models/

cp DelphiAnalysisMcpServer_FIXED/Services/AnalysisRepository.cs \
   /path/to/DelphiAnalysisMcpServer/Services/

cp DelphiAnalysisMcpServer_FIXED/Services/ProjectPersistenceService.cs \
   /path/to/DelphiAnalysisMcpServer/Services/

# Option B: Replace entire directory
# rm -rf /path/to/DelphiAnalysisMcpServer
# cp -r DelphiAnalysisMcpServer_FIXED /path/to/DelphiAnalysisMcpServer
```

### Step 4: Rebuild

```bash
cd /path/to/DelphiAnalysisMcpServer
dotnet build -c Release
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Step 5: Restart Service

```bash
sudo systemctl start delphi-analysis-mcp
sudo systemctl status delphi-analysis-mcp
```

Expected: `Active: active (running)`

### Step 6: Test the Fix

```bash
cd ~/repos/dotnet/Tools/DelphiDocumenter
./bin/Debug/net10.0/DelphiDocumenter \
  -f /srv/sfddevelopment/Source/embarcadero/Embarcadero/Projects/SFD/sfd_fdb \
  -m https://sfddevelopment.com/mcp/delphi-analysis
```

Look for in the summary:
```
Queries Found: 42    ✓  (was 0 before)
Total Units: 10      ✓  (was 0 before)
Total Forms: 2       ✓  (was 0 before)
```

### If Something Goes Wrong

Rollback to backup:
```bash
sudo systemctl stop delphi-analysis-mcp
rm -rf /path/to/DelphiAnalysisMcpServer
mv /path/to/DelphiAnalysisMcpServer.backup.YYYYMMDD_HHMMSS /path/to/DelphiAnalysisMcpServer
sudo systemctl start delphi-analysis-mcp
```

---

## Files Changed

1. `Models/DatabaseEntities.cs` - Added ProjectStatistics class
2. `Services/AnalysisRepository.cs` - Added GetProjectStatisticsAsync method  
3. `Services/ProjectPersistenceService.cs` - Call statistics method and use results

See `CHANGES.md` for detailed explanation of what changed and why.

---

## That's It!

Total time: ~5 minutes
Risk: Low (no database changes, no breaking changes)
Impact: Accurate statistics reporting

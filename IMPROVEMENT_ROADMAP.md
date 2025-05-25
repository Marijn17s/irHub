# irHub Improvement Roadmap

This document outlines major improvements identified for the irHub application, organized by priority and impact.

## Quick Wins

These improvements provide immediate value with relatively low effort:

- [ ] **Extract services from Global.cs** - Start with `ProgramService`
- [ ] **Add comprehensive input validation** - File paths, executable names
- [ ] **Implement proper async/await** throughout the application
- [ ] **Add search functionality** to the program list
- [ ] **Expand the Settings class** with commonly requested options

## 1. Architecture & Code Quality

**Priority: High**

### Current Issues:
- `Global.cs`s handles too many responsibilities
- Static state management makes testing difficult
- Missing dependency injection

### Tasks:
- [ ] **Implement MVVM pattern properly** with ViewModels
- [ ] **Add dependency injection** where possible
- [ ] **Split Global.cs** into focused services:
  - [ ] `ProgramService` for program management
  - [ ] `iRacingService` for SDK interactions  
  - [ ] `SettingsService` for configuration
  - [ ] `ProcessMonitoringService` for process tracking
- [ ] **Create proper interfaces** for all services

## 2. Settings & Configuration System

**Priority: High**

### Current Limitations:
- Very basic Settings class with only `StartMinimized`
- No configuration validation
- Missing many expected settings

### Tasks:
- [ ] **Expand Settings class** with comprehensive options:
  ```csharp
  public class Settings 
  {
      public bool StartMinimized { get; set; }
      public bool ShowGarageCover { get; set; }
      public bool AutoDetectApplications { get; set; }
      public int ProcessCheckInterval { get; set; } = 5000;
      public string Theme { get; set; } = "Dark";
      public bool EnableLogging { get; set; } = true;
      public LogLevel LogLevel { get; set; } = LogLevel.Information;
      public bool StartWithWindows { get; set; }
      public bool MinimizeOnClose { get; set; }
      public string DefaultApplicationPath { get; set; }
  }
  ```
- [ ] **Add settings validation** with proper error messages
- [ ] **Create settings categories** for better organization
- [ ] **Implement settings migration** for version compatibility
- [ ] **Add settings import/export** functionality

## 3. Error Handling & Resilience

**Priority: High**

### Current Issues:
- Basic exception handling
- No retry mechanisms for failed operations
- Limited user feedback on errors

### Tasks:
- [ ] **Add comprehensive exception handling** with user-friendly error dialogs
- [ ] **Implement retry logic** for process operations
- [ ] **Add validation** for file paths and executable existence
- [ ] **Better error recovery** for iRacing SDK disconnections
- [ ] **Create centralized error logging** system
- [ ] **Add user notification system** for errors and warnings
- [ ] **Implement graceful degradation** when services fail

## 4. Performance Optimizations

**Priority: Medium**

### Current Issues:
- Synchronous file operations
- UI thread blocking operations
- Inefficient process monitoring

### Tasks:
- [ ] **Make all file I/O async** with proper cancellation support
- [ ] **Use `IAsyncEnumerable`** for program collections
- [ ] **Implement proper cancellation tokens** throughout the application
- [ ] **Add background service** for process monitoring with configurable intervals
- [ ] **Optimize icon loading** and caching
- [ ] **Implement lazy loading** for program data
- [ ] **Add memory usage optimization** for large program lists

## 5. Feature Enhancements

**Priority: Medium-High**

### Automatic Application Detection:
- [ ] **Create ApplicationDetectionService**:
  ```csharp
  public class ApplicationDetectionService
  {
      public async Task<List<DetectedApplication>> ScanForSimRacingApps()
      {
          // Scan common installation directories
          // Check registry for installed applications
          // Identify by known application signatures
      }
  }
  ```
- [ ] **Build database of known sim racing applications**
- [ ] **Add one-click application import**

### Enhanced UI/UX:
- [ ] **Implement drag-and-drop** for program reordering
- [ ] **Add program grouping/categories**
- [ ] **Implement keyboard shortcuts**
- [ ] **Create program templates** for common applications

### Endurance Planner (Planned Feature):
- [ ] **Design data model** for race planning
- [ ] **Create race schedule UI**
- [ ] **Add driver stint planning**
- [ ] **Implement fuel/tire strategy calculator**
- [ ] **Implement other features from the endurance excel sheet**

### P2P Setup Sharing (Planned Feature):
- [ ] **Design setup sharing protocol**
- [ ] **Create setup metadata system**
- [ ] **Implement secure file transfer (maybe using a websocket)**
- [ ] **Add setup version control**
- [ ] **Add auto-sync between users**

## 6. Data Management

**Priority: Medium**

### Current Issues:
- Simple JSON serialization without schema versioning
- No data migration strategy
- Limited backup/restore functionality

### Tasks:
- [ ] **Add configuration schema versioning** for safe updates
- [ ] **Implement automatic backup system**
- [ ] **Add import/export functionality** for sharing configurations
- [ ] **Consider SQLite migration** for complex data relationships
- [ ] **Implement data validation** on load/save
- [ ] **Add data corruption recovery**

## 7. Testing Infrastructure

**Priority: Medium**

### Currently Missing:
- Unit tests
- Integration tests
- UI automation tests

### Tasks:
- [ ] **Add test project**
- [ ] **Create mock services** for testing
- [ ] **Implement test coverage reporting**
- [ ] **Add integration tests** for iRacing SDK
- [ ] **Create UI automation tests** for critical paths
- [ ] **Set up CI/CD pipeline** with automated testing

## 8. Monitoring & Diagnostics

**Priority: Medium**

### Tasks:
- [ ] **Enhanced structured logging?** (Serilog is already implemented)
- [ ] **Add application performance counters**
- [ ] **Implement health checks** for iRacing SDK connection
- [ ] **Add user analytics?** (anonymized) to understand feature usage
- [ ] **Create diagnostic tools** for troubleshooting
- [ ] **Implement bug reporting system**
- [ ] **Implement crash reporting system**

## 9. Deployment & Updates

**Priority: Low-Medium**

### Current: Velopack is implemented

### Improvements:
- [ ] **Add rollback capability**
- [ ] **Implement delta updates**
- [ ] **Add pre-release/beta channel** option
- [ ] **Better update notifications** with changelog
- [ ] **Implement silent updates** option
- [ ] **Add update scheduling**

## 10. Security

**Priority: Medium**

### Tasks:
- [ ] **Input validation** for all user inputs
- [ ] **Safe file path handling** to prevent path traversal
- [ ] **Code signing** for releases
- [ ] **Antivirus false-positive mitigation**
- [ ] **Implement secure storage** for sensitive settings
- [ ] **Add file integrity checks**

## Implementation Strategy 📋

### Phase 1 (Foundation)
1. Extract `ProgramService` from `Global.cs`
2. Add comprehensive input validation
3. Implement proper async/await patterns
4. Expand Settings class

### Phase 2 (Core Features)
1. Complete service extraction
2. Add dependency injection
3. Implement search functionality
4. Enhanced error handling

### Phase 3 (Advanced Features)
1. Application auto-detection
2. Enhanced UI/UX features
3. Performance optimizations
4. Testing infrastructure

### Phase 4 (Polish & Future)
1. Monitoring and diagnostics
2. Advanced planned features
3. Security enhancements
4. Documentation and guides

## Notes

- **Start with Phase 1** items as they provide the foundation for all other improvements
- **Prioritize user-facing features** that provide immediate value
- **Test thoroughly** before moving to the next phase
- **Consider user feedback** when prioritizing features
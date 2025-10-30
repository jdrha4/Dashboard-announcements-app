/* @Service
public class DashboardSettingsService {
    private final DashboardSettingsRepository repo;

    public DashboardSettingsService(DashboardSettingsRepository repo) {
        this.repo = repo;
    }

    @Transactional
    public DashboardSettingsDto getSettings(Long dashboardId) {
        return repo.findById(dashboardId)
                   .map(this::toDto)
                   .orElseGet(() -> defaults());
    }

    @Transactional
    public DashboardSettingsDto updateSettings(Long dashboardId,
        @Valid DashboardSettingsDto dto) {
        DashboardSettings settings = repo.findById(dashboardId)
            .orElse(new DashboardSettings());
        settings.setDashboardId(dashboardId);
        settings.setMaxExpiryDate(dto.getMaxExpiryDate());
        settings.setMaxAnnouncements(dto.getMaxAnnouncements());
        repo.save(settings);
        return dto;
    }

    private DashboardSettingsDto toDto(DashboardSettings s) {
        DashboardSettingsDto dto = new DashboardSettingsDto();
        dto.setMaxExpiryDate(s.getMaxExpiryDate());
        dto.setMaxAnnouncements(s.getMaxAnnouncements());
        return dto;
    }

    private DashboardSettingsDto defaults() {
        DashboardSettingsDto dto = new DashboardSettingsDto();
        dto.setMaxExpiryDate(LocalDate.now().plusMonths(1));
        dto.setMaxAnnouncements(50);
        return dto;
    }
}
 */

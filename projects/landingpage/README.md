# PinedaTec Landing Page

A modern, professional landing page built with .NET Blazor and Blazorise, following Domain-Driven Design (DDD) architecture principles.

## Architecture

This project implements a clean DDD architecture with clear separation of concerns:

- **Domain Layer** (`landingpage.domain`): Contains entities, value objects, and interfaces
- **Application Layer** (`landingpage.application`): Business logic and services
- **Infrastructure Layer** (`landingpage.infrastructure`): Data access and external integrations
- **Presentation Layer** (`landingpage.portal`): Blazor UI components

## Features

### Core Functionality
- ✅ Paginated article listing with Previous/Next navigation
- ✅ Article cards with title, date, excerpt, tags, and links
- ✅ Responsive design (mobile, tablet, desktop)
- ✅ Professional PinedaTec color scheme (dark tones with blue/orange accents)

### Internationalization
- ✅ Bilingual support (English/Spanish)
- ✅ Language selector in navbar
- ✅ All UI texts localized using resource files

### Navigation
- ✅ Responsive navbar with logo
- ✅ "Asistente Markus" (SLM agent integration placeholder)
- ✅ LinkedIn profile link
- ✅ LinkedIn newsletter link
- ✅ Footer with contact and privacy links

### Design
- Dark elegant theme matching PinedaTec branding
- Bootstrap 5 with Blazorise components
- Font Awesome icons
- Smooth hover effects and transitions
- Card-based article layout

## Technology Stack

- **.NET 10.0**: Latest .NET framework
- **Blazor Server**: Interactive server-side UI
- **Blazorise**: UI component library with Bootstrap 5
- **xUnit**: Unit testing framework
- **Moq**: Mocking framework for tests

## Getting Started

### Prerequisites
- .NET 10.0 SDK or later
- Any modern web browser

### Running the Application

1. Navigate to the portal directory:
```bash
cd projects/landingpage/landingpage.portal
```

2. Run the application:
```bash
dotnet run
```

3. Open your browser and navigate to `https://localhost:5001` (or the URL shown in the console)

### Running Tests

Navigate to the test project and run:
```bash
cd projects/landingpage/landingpage.tests
dotnet test
```

## Project Structure

```
landingpage/
├── landingpage.domain/          # Domain entities and interfaces
│   ├── Entities/                # Article entity
│   ├── ValueObjects/            # PagedResult
│   └── Interfaces/              # IArticleRepository
├── landingpage.application/     # Application services
│   └── Services/                # ArticleService
├── landingpage.infrastructure/  # Infrastructure implementations
│   └── Repositories/            # MockArticleRepository
├── landingpage.portal/          # Blazor web application
│   ├── Components/
│   │   ├── Layout/             # MainLayout with Navbar and Footer
│   │   └── Pages/              # Home page with article listing
│   ├── Resources/              # Localization resources (en, es)
│   └── wwwroot/                # Static assets and CSS
└── landingpage.tests/          # Unit tests
    └── ArticleServiceTests.cs
```

## Localization

The application supports English and Spanish. Resource files are located in:
- `landingpage.portal/Resources/Shared.resx` (English)
- `landingpage.portal/Resources/Shared.es.resx` (Spanish)

To add a new language:
1. Create a new resource file: `Shared.[culture].resx`
2. Add the culture to the supported cultures list in `Program.cs`

## Future Enhancements

- Connect to real API endpoint (replace `MockArticleRepository`)
- Add more languages
- Implement article categories and filters
- Add search functionality
- Integrate local SLM agent for "Asistente Markus"
- Add article detail pages
- Implement contact and privacy policy pages

## Testing

The project includes comprehensive unit tests for the article service covering:
- Pagination logic
- Input validation
- Article retrieval
- Edge cases

All tests pass successfully:
```
Test Run Successful.
Total tests: 5
     Passed: 5
```

## License

© 2024 PinedaTec. All rights reserved.

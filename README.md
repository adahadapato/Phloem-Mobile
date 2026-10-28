# Phloem mobile app

The **Phloem** app for Android and iOS (also builds for Windows and macOS): African food from home cooks and food
businesses in **Ghana, Nigeria and the United Kingdom**, plus a parcel service for sending food between them.

Built with **.NET MAUI (.NET 10)** and **MVVM** (CommunityToolkit.Mvvm). All data comes from the **Phloem API**
(`https://phloemapi.phloemmarket.com`), the same API as the website ([phloemmarket.com](https://phloemmarket.com)),
so prices, orders, texts and settings are always the same everywhere.

| | Repository | Branch |
|---|---|---|
| Mobile app (this) | `adahadapato/phloem-mobile` | **`development`** (work) → `main` |
| API (.NET 10) | `adahadapato/Phloem-Api` | `main` |
| Website (React) | `adahadapato/Phloem-web` | `main` |

---

## Contents

1. [What's in the app](#1-whats-in-the-app)
2. [Tech stack](#2-tech-stack)
3. [Getting started](#3-getting-started)
4. [Configuration](#4-configuration)
5. [Project structure](#5-project-structure)
6. [Navigation](#6-navigation)
7. [How it works](#7-how-it-works)
8. [Design system](#8-design-system)
9. [Conventions](#9-conventions)
10. [Building and releasing (CI/CD)](#10-building-and-releasing-cicd)
11. [Troubleshooting](#11-troubleshooting)
12. [Roadmap](#12-roadmap)

---

## 1. What's in the app

**Start-up** — a poster screen loads the countries and restores the sign-in. After the first start, the countries
kept on the phone let the app open offline.

**Buyers**
- **Home**: country picker, search bar with **predictions while typing** (dish names, then shops), "Order again" and
  "You might like" (each card with its reason) or "Popular in Ghana", category chips, popular dishes, top sellers,
  and the "Send food to the UK" banner.
- **Explore** (Browse & Order): dishes by country, category, search and sort; a shop's menu.
- **Orders** tab: the basket and recent orders; **Checkout** with today's prices, closed-shop checks, delivery time,
  delivery address (with recipient name and phone) and one order per shop; order details, cancel, "Buy it again".
- **Send Food to UK**: route, speed, items, "what can be sent", live quote, shipping terms; **My Shipments** with a
  timeline.
- **Account**: profile and photo, delivery addresses, notifications, password, sign out. Sign-in asks for the
  two-factor code when it's switched on (setting it up is done on the website).
- **Help & support**, **Terms**, **Privacy Policy** (from the API) and **Contact us** (form, email, WhatsApp, phone).
- **"We've updated our Terms"** when new Terms are published (browse freely; ordering, parcels and selling wait
  until accepted) and a one-time Privacy notice.

**Sellers** — Become a Seller (with "I agree to the Seller terms"), Seller Dashboard (incoming orders, daily sales),
add and edit dishes with photos and allowed price ranges, shop settings (logo, cover, delivery fee, order cut-off,
documents).

**Admins / staff** — Admin Dashboard (Sellers, Users, Shipments tabs): approve shops and review documents, users and
roles, suspend / reinstate, dish names and price ranges, **Pricing** (commission, delivery fees, delivery times),
shipping routes and rates, what can be sent, all shipments (receive and weigh at the hub, next step).
Terms editing and the Contact messages inbox are on the website.

---

## 2. Tech stack

| Area | Choice | Version |
|---|---|---|
| Framework | .NET MAUI (single project) | .NET 10, Microsoft.Maui.Controls 10.0.110 |
| MVVM | CommunityToolkit.Mvvm (`[ObservableProperty]` partial properties, `[RelayCommand]`) | 8.4.2 |
| HTTP | `IHttpClientFactory` (Microsoft.Extensions.Http) with a token handler | 10.0.12 |
| Settings | Microsoft.Extensions.Configuration.Json + Options (embedded `appsettings.json`) | 10.0.12 |
| Token storage | MAUI `SecureStorage` | — |
| Navigation | .NET MAUI Shell (tabs + flyout + routes) | — |

| Target | Framework | Minimum |
|---|---|---|
| Android | `net10.0-android` | Android 5.0 (API 21) |
| iOS | `net10.0-ios` | iOS 15 |
| macOS | `net10.0-maccatalyst` | macOS (Catalyst) 15 |
| Windows | `net10.0-windows10.0.19041.0` (only when building on Windows) | Windows 10 1809 |

App id **`com.zayun.phloem`** · title **Phloem** · version **1.0 (1)**.

---

## 3. Getting started

### 3.1 Prerequisites

- **.NET 10 SDK** and the MAUI workload: `dotnet workload install maui`
  (or only Android: `dotnet workload install maui-android`).
- **Visual Studio 2022 17.14+ / 2026** with ".NET Multi-platform App UI development", or **Rider**, or VS Code with
  the .NET MAUI extension.
- **Android**: the Android SDK and an emulator (Visual Studio installs both) or a phone with USB debugging.
- **iOS / Mac**: a Mac with Xcode (or Visual Studio paired to a Mac).
- To work against a **local API**: the Phloem API running with the `https` profile (see its README).

### 3.2 Clone and run

```bash
git clone https://github.com/adahadapato/phloem-mobile.git AfroPhloem
cd AfroPhloem
git checkout development
dotnet restore

dotnet build -t:Run -f net10.0-android                    # Android emulator / phone
dotnet build -t:Run -f net10.0-ios                        # iOS simulator (Mac)
dotnet build -t:Run -f net10.0-maccatalyst                # Mac desktop
dotnet build -t:Run -f net10.0-windows10.0.19041.0        # Windows desktop
```

Or open `AfroPhloem.sln` in Visual Studio, pick the target next to Run, and press **F5**.

Release builds talk to the **live API**. Debug builds can talk to your **local API**: see section 4.

---

## 4. Configuration

Settings are JSON files **embedded in the app** (`Phloem.csproj`):

| File | Used in | Committed |
|---|---|---|
| `Settings/appsettings.json` | every build | yes — the live API |
| `Settings/appsettings.Development.json` | **Debug builds only**, on top of the above | **no** (git-ignored; each developer makes their own) |

`Settings/appsettings.json`:

```json
{
  "Api": {
    "BaseUrl": "https://phloemapi.phloemmarket.com",
    "AndroidEmulatorBaseUrl": "",
    "AndroidEmulatorFilesBaseUrl": "",
    "TimeoutSeconds": 30,
    "TrustDevelopmentCertificate": false
  },
  "Paging": {
    "SizeOptions": [ 10, 15, 20, 30, 50 ],
    "Defaults": { "Dishes": 20, "Shops": 10, "MyOrders": 10, "MyShipments": 10, "SellerOrders": 15,
                  "SellerDishes": 15, "AdminShops": 20, "AdminUsers": 20, "AdminShipments": 20, "AdminDishNames": 20 }
  }
}
```

**Local API** — create `Settings/appsettings.Development.json`:

```json
{
  "Api": {
    "BaseUrl": "https://localhost:7103",
    "AndroidEmulatorBaseUrl": "https://10.0.2.2:7103",
    "AndroidEmulatorFilesBaseUrl": "http://10.0.2.2:5175",
    "TrustDevelopmentCertificate": true
  }
}
```

| Key | Meaning |
|---|---|
| `Api:BaseUrl` | the API address, no trailing slash |
| `Api:AndroidEmulatorBaseUrl` | used only on the Android emulator (`10.0.2.2` is your PC from inside the emulator) |
| `Api:AndroidEmulatorFilesBaseUrl` | emulator only: where pictures load from (the local API's http port, because the image loader doesn't accept the dev certificate) |
| `Api:TimeoutSeconds` | how long one request may take |
| `Api:TrustDevelopmentCertificate` | Debug only: accept the local development certificate |
| `Paging` | page-size choices and defaults per list |

**A real phone** can't reach "localhost" on your PC: put your PC's network address in `BaseUrl`
(e.g. `https://192.168.1.20:7103`) and start the API listening on that address.

---

## 5. Project structure

```
AfroPhloem/
├── AfroPhloem.sln · Phloem.csproj
├── MauiProgram.cs           app builder: fonts, settings, services (calls DependencyInjection)
├── DependencyInjection.cs   every service, view model and page (one place)
├── App.xaml(.cs)            app resources; opens StartupPage
├── AppShell.xaml(.cs)       bottom tabs, side menu, routes, menu items by role
├── AppNav.cs                shared navigation commands (Home, Terms/Privacy/Help at a section, Contact,
│                            "We've updated our Terms"…)
├── GlobalUsings.cs
├── Settings/                appsettings.json (+ your Development file), ApiSettings, PagingSettings, AppSettings keys
├── Services/
│   ├── Api/                 ApiClient, ApiResult/ApiError, TokenStore (SecureStorage), token handler, ApiQuery,
│   │   └── Contracts/       Dtos/, Commands/, Enums/, Queries/ — same shapes as the API
│   ├── AuthService, SessionService, AccountService      sign-in, who is signed in, my account
│   ├── CountryService, DishService, ShopService, SuggestionService
│   ├── CartService, OrderService, DeliveryAddressService, OrderHistoryService
│   ├── ShipmentService, ShipmentRouteService, ParcelRulesService
│   ├── SellerService, DishCatalogService
│   ├── AdminService, PricingService
│   ├── LegalService, ContactService, TermsPromptService, ExternalSignIn
│   └── MockDataService      (legacy from the first prototype; no screen uses it any more)
├── Models/                  view models' item types (Dish, Vendor, CartItem, CountryOption, SuggestedDish,
│                            SearchPrediction, ShipmentCard, ShopOrderCard, AdminCards…)
├── ViewModels/              one per screen (HomeViewModel, CheckoutViewModel, AdminPricingViewModel…)
├── Views/                   one XAML page per screen (+ Controls/ e.g. StatusBanner, PagerView)
├── Converters/              IntToBool, InvertedBool, StringNotEmpty, RelativeDate
├── Helpers/                 SimpleMarkdown (renders Terms/Privacy/Help)
├── Resources/
│   ├── Styles/Colors.xaml   the brand palette (the website's tokens.css uses the same values)
│   ├── Styles/Styles.xaml   buttons, cards, labels, fields
│   ├── Images/              pictures, plus appicon.png / appiconfg.png (app icon) and splash.png
├── Platforms/               Android, iOS, MacCatalyst, Windows
└── .github/workflows/       android-build.yml, android-release.yml, ios-build.yml
```

### Screens

| Area | Pages |
|---|---|
| Start | `StartupPage` |
| Tabs | `HomePage`, `FoodListingPage` (Explore), `OrdersPage`, `AccountPage` |
| Buying | `CheckoutPage`, `OrderDetailsPage`, `DeliveryAddressPage`, `VendorsListPage` |
| Parcels | `InternationalDeliveryPage` (Send Food to UK), `MyShipmentsPage`, `ShipmentDetailsPage` |
| Account | `LoginPage`, `RegisterPage`, `EditProfilePage`, `ChangePasswordPage`, `NotificationSettingsPage` |
| Texts & help | `AboutPage`, `LegalPage` (Terms / Privacy / Help), `ContactPage`, `TermsUpdatePage` |
| Selling | `SellerSetupPage`, `SellerDashboardPage`, `DishEditorPage`, `ShopOrderDetailsPage`, `ShopSettingsPage` |
| Admin | `AdminPage`, `AdminShopDetailsPage`, `AdminDishCatalogPage`, `AdminPricingPage`, `AdminShipmentRoutesPage`, `AdminParcelRulesPage`, `AdminShipmentsPage` |

---

## 6. Navigation

- **Bottom tabs** (always visible): **Home · Explore · Orders · Account**.
- **Side menu (☰)**: About · Help & support · Contact us | Admin Dashboard · Send Food to UK · Sellers & Vendors ·
  Become a Seller / Seller Dashboard · My Shipments · Sign Up. Items are shown or hidden by who is signed in
  (`AppShell.UpdateMenu`, refreshed whenever the session changes).
- Every other page opens **on top of a tab** (the bottom bar stays): routes are registered in `AppShell.xaml.cs`
  (`about`, `legal`, `contact`, `termsupdate`, `login`, `register`, `admin`, `adminpricing`, `international`,
  `CheckoutPage`…).
- Query parameters: `legal?type=Help&section=parcels` opens Help at a section; `//listing?search=jollof` opens Explore
  searched; `//listing?vendorId=…&vendorName=…` opens a shop's menu.
- Shared commands live in `AppNav` (e.g. `AppNav.OpenHelpCommand`, `AppNav.OpenParcelTermsCommand`,
  `AppNav.OpenContactCommand`, `AppNav.OpenTermsUpdateAsync()`), so any XAML can use
  `{x:Static phloem:AppNav.…}`.

---

## 7. How it works

### 7.1 Talking to the API

- **`ApiClient`** is the one way to call the API: `GetAsync<T>`, `PostAsync`, `PutAsync`, `DeleteAsync` and file
  uploads. It never throws for HTTP problems: every call returns an **`ApiResult` / `ApiResult<T>`** with either a
  value or an **`ApiError`** (`Status`, `Code`, `Message`, `FieldErrors`, `DisplayMessage`).
- A delegating handler adds the access token and **renews it** when it's about to run out (or once after a 401).
  If renewal is refused, the person is signed out (`TokenStore.SessionExpired`).
- One **service per API area** turns the API's DTOs into the app's models, so the screens bind to familiar types
  (`Dish`, `Vendor`, `CartItem`…).
- `Contracts/` mirror the API's DTOs and commands **exactly** (enums as names). Keep them in step with the API and
  the website's `src/api/contracts`.

### 7.2 Signing in

- Email + password (with two-factor when switched on), "Remember me", register, forgot password.
- Tokens are kept in **`SecureStorage`** (Keychain on iOS, Keystore-backed on Android).
- `SessionService` holds the signed-in profile and roles and raises `Changed`, which the side menu and screens listen
  to. `AuthService.RefreshProfileAsync()` reloads the profile after a change.
- Google sign-in (`ExternalSignIn`) uses the system browser and returns to **`phloem://auth`**; it's ready on the API
  side and switched on when the keys are added.

### 7.3 Things that come from the API (no app update needed)

- Countries, flags, currencies and parcel routes (`GET /api/countries`, also kept on the phone for offline start).
- Terms, Privacy Policy and Help (rendered by `Helpers/SimpleMarkdown`; headings carry anchors such as
  `{#orders}`, `{#parcels}`, `{#sellers}` that the app links to).
- The "what can be sent" list, delivery times and charges, price ranges, contact details.

### 7.4 Terms acceptance

`TermsPromptService` checks the profile's `terms` status whenever Home appears: if new Terms need accepting it opens
**`TermsUpdatePage`** (once per app run; "Not now" lets people keep browsing), and if the Privacy Policy changed it
shows a one-time notice. When the API refuses an action with `Terms.NotAccepted` (checkout, adding a dish), the same
screen opens and the person tries again after accepting.

---

## 8. Design system

- **`Resources/Styles/Colors.xaml`** is the brand palette (`BrandDark #043D1F`, `BrandGreen #046334`,
  `BrandOrange #E23902`, `BrandGold #F4A51A`, `BrandCream #F8F5EF`, `BrandMint`, `BrandPeach`, `BrandBorder`,
  `BrandTextDark`, `BrandTextMuted`…). The website's `tokens.css` uses the same values: change both together.
- **`Resources/Styles/Styles.xaml`**: `CtaButton`, `PrimaryButton`, `SecondaryButton`, `OutlineButton`,
  `CircleIconButton`, `DishCard`, `VendorCard`, `CardBorder`, `RatingBadge`, `SectionTitle`, `SubtleLabel`,
  `LinkLabel`, `PriceLabel`, `EyebrowLabel`, `HeroTitle`, `StatTile`…
- Every screen uses the same **green header with a back arrow and the Phloem logo** (tapping the logo goes Home).
- The **splash screen** is fixed (don't change it).

---

## 9. Conventions

- **One view model per screen**, registered in `DependencyInjection.cs` with its page (both transient; services are
  singletons).
- `[ObservableProperty] public partial …` properties and `[RelayCommand]` instance methods (not static).
- **Compiled bindings**: every page and `DataTemplate` sets `x:DataType`.
- Words people read come from the API where possible (`ApiError.DisplayMessage`, texts, contact details); otherwise
  short, plain and friendly.
- A list that couldn't load never closes the app: show a message (or hide an optional section) instead.
- **Work on `development`**; `main` gets merged releases.

---

## 10. Building and releasing (CI/CD)

| Workflow | When | What |
|---|---|---|
| `android-build.yml` — *Phloem Android build check* | push to `development`, pull request to `main` | installs the MAUI Android workload and builds the Android app (no signing) |
| `android-release.yml` — *Phloem Android Release* | manual (Run workflow) | publishes a **signed APK and AAB** (`dotnet publish -f net10.0-android -c Release`) and uploads both as artifacts |
| `ios-build.yml` | — | **needs fixing**: the file currently holds a copy of the project file, not a workflow |

**Secrets for the Android release**

| Secret | |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | the upload keystore, base64-encoded |
| `ANDROID_KEYSTORE_PASSWORD` | keystore password |
| `ANDROID_KEY_ALIAS` | key alias |
| `ANDROID_KEY_PASSWORD` | key password |

Keep the keystore and its passwords safe: Google Play only accepts updates signed with the same key.

**Before a release:** bump `ApplicationDisplayVersion` / `ApplicationVersion` in `Phloem.csproj`, make sure
`Settings/appsettings.json` points at the live API, and deploy any API changes the release depends on **first**.

---

## 11. Troubleshooting

| Problem | Fix |
|---|---|
| Android emulator: "couldn't reach Phloem" | use `AndroidEmulatorBaseUrl` `https://10.0.2.2:7103` and `TrustDevelopmentCertificate: true` in your Development settings; start the API with the `https` profile |
| Emulator: pictures don't load | set `AndroidEmulatorFilesBaseUrl` to `http://10.0.2.2:5175` |
| Real phone can't reach the local API | use your PC's network address, not localhost; allow it through the firewall |
| First start offline: "can't load" | the first start needs internet to fetch the countries; later starts work offline |
| "Please read and agree to the Seller terms" | tick the box above "Submit seller profile" (required since the Terms update) |
| Build error about a missing workload | `dotnet workload install maui` (or `maui-android`) |
| Release build signing fails | check the four Android secrets and that the keystore decodes |

---

## 12. Roadmap

- **"Chat with us"** AI assistant (answers from Phloem's own data and Help; hand-over to the team).
- **Google sign-in** (waiting for keys).
- A real **payment provider** instead of the temporary test payment.
- App versions of **Admin → Contact messages** and Terms acceptance figures (on the website today), search
  predictions on the Explore tab, and Edit / Delete / Make primary on delivery addresses.
- A working **iOS build** workflow.

---

**Licence:** not specified yet. © Phloem.

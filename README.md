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
- **Sign in with Face ID / fingerprint** (everyone, admins and staff included): offered after a login, or switched
  on in Account → Settings. See [7.3](#73-sign-in-with-face-id--fingerprint).
- **Help & support**, **Terms**, **Privacy Policy** (from the API) and **Contact us** (form, email, WhatsApp, phone).
- **"We've updated our Terms"** when new Terms are published (browse freely; ordering, parcels and selling wait
  until accepted) and a one-time Privacy notice.

**Sellers** — Become a Seller (with "I agree to the Seller terms"), Seller Dashboard (incoming orders, daily sales),
add and edit dishes with photos and allowed price ranges, shop settings (logo, cover, delivery fee, order cut-off,
documents). **Send out** asks who's delivering it: a courier in their country, or "I'll deliver it myself".

**Couriers** — **My Deliveries** (side menu): food orders and parcels to deliver, with where to collect, who to call
and the address (opens the maps app). **Delivered** opens the camera: a photo is always required, plus how it was
handed over (in person, front door, neighbour, safe place, reception) and an optional note. The customer sees the
photo on their order page (**Proof of delivery**).

**Keep screen on** — while the Seller Dashboard, an order, My Deliveries, tracking or Checkout is open, the phone
doesn't dim or lock (`ScreenAwakeService` + `KeepScreenAwakeBehavior`). Switch it off in Account → Settings.

**Admins / staff** — Admin Dashboard (Sellers, Users, Shipments tabs): approve shops and review documents, users and
roles, suspend / reinstate, dish names and price ranges, **Pricing** (commission, delivery fees, delivery times),
shipping routes and rates, what can be sent, all shipments (receive and weigh at the hub, next step, out for delivery
with a courier). Give someone the **Courier** role on Admin → Users.
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
| Android | `net10.0-android` | Android 6.0 (API 23): the AndroidX libraries need it (raised from 5.0 in Nov 2026) |
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
│   ├── Biometrics/          Face ID / fingerprint sign-in: IBiometricService, BiometricSignIn, BiometricSettings,
│   │                        BiometricStatus, BiometricOutcome, NoBiometricService (Windows / Mac)
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
│                            (Android/Biometrics and iOS/Biometrics: each phone's own fingerprint / face check)
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
| Delivering | `MyDeliveriesPage`, `DeliveryProofPage` (camera + handover); `Controls/DeliveryProofCard` on order and parcel pages |
| Admin | `AdminPage`, `AdminShopDetailsPage`, `AdminDishCatalogPage`, `AdminPricingPage`, `AdminShipmentRoutesPage`, `AdminParcelRulesPage`, `AdminShipmentsPage` |

---

## 6. Navigation

- **Bottom tabs** (always visible): **Home · Explore · Orders · Account**.
- **Side menu (☰)**: About · Help & support · Contact us | Admin Dashboard · Send Food to UK · Sellers & Vendors ·
  Become a Seller / Seller Dashboard · My Shipments · My Deliveries (couriers and sellers) · Sign Up. Items are shown or hidden by who is signed in
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

### 7.3 Sign in with Face ID / fingerprint

**What it is.** A scan unlocks the sign-in that is already kept on the phone; it is not a new kind of sign-in, and the
API needs nothing new. The first sign-in on a phone is always a normal one (password or Google, plus the two-step
code when it's on). Available to every account, admins and staff included.

**Switching it on**
- After a password or Google login, a pop-up asks "Sign in with Face ID?" (`ViewModels/BiometricOptInPrompt`).
  "Not now" is remembered for that account, so it isn't asked at every login.
- Or **Account → Settings → Sign in with Face ID / fingerprint** (a switch). Turning it on asks for one scan.
- Switching it on keeps the sign-in on the phone even if "Remember me" wasn't ticked.

**Using it**
- **App start:** the scan is asked for before the saved sign-in is loaded. Closing the prompt opens the app signed
  out.
- **Log in page:** the scan comes FIRST. The prompt opens by itself (once per visit) and there's a "Sign in with …"
  button to try again; the password form is the fallback if the scan fails or is cancelled.
- **Log out only locks** while it's on: the session ends in the app, but the sign-in stays on the phone (still valid
  on the server), so the next login is a scan. The Log out pop-up says so.
- Only the phone's biometrics are accepted, never the phone's PIN / passcode (someone who knows the PIN must still
  type the Phloem password). "Use password" closes the prompt.

**What the phone can do** (`BiometricStatus`, checked fresh each time)

| Phone | What the person sees |
|---|---|
| No sensor (or Android older than 7.0, Windows, Mac) | Nothing: no pop-up, no button, no Settings row. |
| Sensor, but no fingerprint / face set up | No pop-up. The Settings row is greyed out with a hint; on Android, tapping it opens the phone's "add a fingerprint" screen, and the row is checked again on return. iOS doesn't allow opening that screen, so it only says where to go. |
| Fingerprint / face set up | Everything works. |

**When it switches off**
- **The My account switch** is turned off (then Log out works normally again: the sign-in is cancelled on the server
  and deleted from the phone).
- **Password changed** in the app (`LogoutAsync(forgetThisPhone: true)`), or the sign-in can't be renewed (unused for
  30 days, or ended on the server: a password change or reset elsewhere ends every session).
- **Someone else signs in** on the phone (the locked sign-in of the previous account is cancelled on the server
  first).
- **The phone's fingerprints / faces change** (one added, or all removed): the saved sign-in is also deleted, and the
  person sees "Your phone's fingerprint or face settings changed, so please sign in with your password." They can
  switch it back on afterwards.

**How it's built**
- `Services/Biometrics/BiometricSignIn` holds the rules above; `AuthService.RestoreSessionAsync` (start-up) and
  `AuthService.SignInWithBiometricsAsync` (Log in button) use it. The on/off choice is in `Preferences`
  (`BiometricSettings`); the tokens stay in `SecureStorage` (`TokenStore`), as with "Remember me".
- **Android** (`Platforms/Android/Biometrics`): AndroidX `BiometricPrompt` (package `Xamarin.AndroidX.Biometric`),
  STRONG biometrics only (fingerprint and secure face sensors). A Keystore key is made when it's switched on; Android
  lets it be used only right after a scan and destroys it when the fingerprints / faces change, which is how a change
  is detected. Each scan is tied to the key, so it can't be faked. Permission `USE_BIOMETRIC` (no pop-up).
- **iOS** (`Platforms/iOS/Biometrics`): LocalAuthentication, biometrics only (no passcode fallback). The phone's
  biometric "state" is saved when it's switched on and compared before every scan. Info.plist needs
  `NSFaceIDUsageDescription`.
- **Privacy:** the check happens on the phone; Phloem never receives fingerprint or face data.

**Testing it**
- Android emulator: Settings → Security → set a screen lock, then add a fingerprint; touch the sensor with the
  emulator's Extended controls → Fingerprint. Adding a second fingerprint afterwards should trigger the "settings
  changed" message.
- iOS simulator: Features → Face ID → Enrolled, then Matching Face / Non-matching Face.

### 7.4 Sign-in for sellers and staff (stricter)

Agreed 24 Nov 2026. The API decides, from the person's roles, and sends `sessionKind` + `lockAfterMinutes` with
every sign-in and renewal (strictest role wins):

| `sessionKind` | Who | Kept on the phone when the app closes? | Locks after … in the background | API ends the sign-in |
|---|---|---|---|---|
| `Staff` | Admin + every staff role | No (only behind the fingerprint / Face ID scan) | 15 min | 12 h after signing in |
| `Seller` | Seller, Courier | No (only behind the scan) | 60 min | 7 days after signing in |
| `Buyer` | everyone else | Yes, with "Remember me" | never | 30 days unused |

- **App closed:** `TokenStore.SaveAsync` never writes a Seller / Staff sign-in to the phone unless quick sign-in is
  on (`TokenStore.KeepStrictOnDevice`, set by `BiometricSignIn`). So reopening the app means signing in again, or a
  scan when quick sign-in is on. A buyer who becomes a seller loses the saved copy at the next renewal.
- **Background lock:** `Services/SessionLock` (attached to the window in `App.CreateWindow`) notes when the app goes
  to the background; on return after the limit it calls `AuthService.LogoutAsync()` (locks with quick sign-in on,
  otherwise signs out completely) and opens Log in with a short message.
- **The 12 h / 7 day limit is the API's** and counts from the password / two-step / Google sign-in; a scan doesn't
  restart it. Past it, the next renewal fails and the person signs in with their password.
- Switching quick sign-in off during a seller / staff sign-in removes the saved copy.
- The numbers are API settings (`Jwt:StaffSessionHours`, `SellerSessionHours`, `StaffIdleMinutes`,
  `SellerIdleMinutes`): changing them needs no app update.

### 7.5 Things that come from the API (no app update needed)

- Countries, flags, currencies and parcel routes (`GET /api/countries`, also kept on the phone for offline start).
- Terms, Privacy Policy and Help (rendered by `Helpers/SimpleMarkdown`; headings carry anchors such as
  `{#orders}`, `{#parcels}`, `{#sellers}` that the app links to).
- The "what can be sent" list, delivery times and charges, price ranges, contact details.

### 7.6 Terms acceptance

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
| No "Sign in with fingerprint" row in Account | the phone has no STRONG biometric sensor (some face unlock is only "weak" on Android), or Android is older than 7.0 |
| "Sign in with fingerprint" button gone from Log in | it was switched off (log out, expired sign-in, fingerprints changed): sign in with the password and switch it on again |

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

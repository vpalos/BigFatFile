# BigFatFile: Product Roadmap

Version: 1.0
Status: Planning
This document outlines the planned and researched features for the BigFatFile platform. The primary goal is to build a secure, high-performance, and enterprise-ready file storage and collaboration service. The roadmap is divided into key functional areas.

## Phase 1: Core B2B Collaboration

These features are designed to transition BigFatFile from a personal storage tool into a true multi-user business platform.

- ### User & Group Management
  - **Description:** Allow tenant administrators to invite new users to their workspace. Create user groups (e.g., "Marketing," "Engineering") to simplify permission management.
  - **Business Value:** This is the foundational feature for team collaboration and a core requirement for any B2B sale.

- ### Granular Access Controls
  - **Description:** The access_rules table will be extended to allow grantee_id to refer to either a user or a group. This will enable permissions to be set on a per-folder or per-file basis for entire teams at once.
  - **Business Value:** Provides the detailed security controls that businesses need to manage access to sensitive information across different departments.

## Phase 2: Enterprise Integration & Security

These features focus on integrating with existing enterprise systems and meeting advanced security and compliance requirements.

- ### Single Sign-On (SSO) & Directory Integration
  - **Description:** Integrate with standard identity providers like **Okta, Azure Active Directory (AD), and SAML 2.0**. This will allow users to log in using their existing company credentials.
  - **Business Value:** A critical requirement for selling to mid-to-large enterprises, as it simplifies user management and enhances security by enforcing company-wide authentication policies.
  - **Suggested Implementation:**
    - **Phase 1: Tenant-Specific Authentication Configuration**
      - Create `TenantAuthenticationSettings` model and repository to store per-tenant SSO settings.
      - Build UI for tenant admins to configure authentication providers (initially LDAP/Active Directory).
      - Securely store and encrypt sensitive credentials (e.g., LDAP bind passwords).
    - **Phase 2: LDAP/Active Directory Login Flow**
      - Develop a tenant-aware login page that displays a username/password form if LDAP is configured.
      - Create a dedicated backend API endpoint to handle LDAP authentication requests.
      - Implement the logic to connect to an external LDAP server and validate user credentials.
    - **Phase 3: Just-In-Time (JIT) User Provisioning**
      - On successful LDAP login, implement the "find-or-create" logic for `User` and `Account` models.
      - Automatically add the provisioned user to the `tenant_members` table with a default role.

- ### Anti-Malware & Threat Protection
  - **Description:** Integrate server-side malware scanning for all uploads. The BFF agent utility will also incorporate real-time threat detection to scan files before they are uploaded or downloaded, preventing the spread of malicious software.
  - **Business Value:** Provides a critical layer of security that builds customer trust and helps meet compliance standards. This is a powerful differentiator.

- ### "Bring Your Own Storage" (BYOS)
  - **Description:** Allow enterprise customers to connect their own S3-compatible storage accounts (e.g., their corporate AWS S3 bucket or Cloudflare R2 account). BigFatFile would manage the metadata and access, while the file data resides in the customer's own cloud.
  - **Business Value:** Appeals to large enterprises with strict data residency or governance requirements, giving them ultimate control over their raw data.

## Phase 3: Advanced Architecture & Analytics

These features represent major long-term investments in the platform's core technology and value proposition.

- ### Content-Addressable Storage (CAS) via CDC
  - **Description:** Evolve the storage backend to a highly efficient Content-Defined Chunking model. This will dramatically reduce storage costs and bandwidth usage for large files.
  - **Status:** A detailed technical architecture document has been drafted. This is a major engineering initiative planned for a future release.
  - **Business Value:** Unlocks significant performance gains and cost savings, enabling the service to compete effectively in markets that handle very large datasets (e.g., video production, scientific research).

- ### Audit Reporting
  - **Description:** Build a user-facing UI on top of the existing `audit` hypertable. This will allow tenant administrators to search, filter, and export detailed activity reports for compliance and security reviews.
  - **Business Value:** Turns the audit log from a backend feature into a high-value, premium product that businesses will pay for to meet their compliance obligations.

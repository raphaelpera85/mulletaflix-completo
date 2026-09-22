// SPDX-FileCopyrightText: 2026 Kilian von Pflugk
// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-FileCopyrightText: 2026 AbandonedCart
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("IntroSkipper.Integration.Tests")]

// The migration tool restores the file version recorded in a pre-MariaDB database, which
// only the migration path may write through a tracked entity.
[assembly: InternalsVisibleTo("mulletaflix-introskipper-migrate")]

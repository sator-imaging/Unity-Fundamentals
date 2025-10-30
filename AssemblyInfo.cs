using System.Runtime.CompilerServices;


/* =====  BASIC  ===== */

//[assembly: Preserve]
//[assembly: InternalsVisibleTo("SatorImaging.Tests.UnityFundamentals")]


/* =====  SPECIAL ACCESSIBILITY  ===== */

//[assembly: InternalsVisibleTo(nameof(SatorImaging) + "." + nameof(SatorImaging.________))]


/* =====  TESTS AND EDITORS  ===== */

#if UNITY_EDITOR

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

[assembly: InternalsVisibleTo(nameof(SatorImaging) + "." + nameof(SatorImaging.UnityFundamentals)
    + ".Editor")]

[assembly: InternalsVisibleTo(nameof(SatorImaging) + ".Tests." + nameof(SatorImaging.UnityFundamentals))]
[assembly: InternalsVisibleTo(nameof(SatorImaging) + ".Tests." + nameof(SatorImaging.UnityFundamentals)
    + ".Editor")]

#endif

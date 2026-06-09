# Micro-FAST: Scale Proportional Aircraft Sizer

Micro-FAST is a lightweight, high-fidelity conceptual aircraft sizing application built using C# and Windows Forms. Designed specifically for sub-scale Radio Control (RC) aircraft, unmanned aerial vehicles (UAVs), and cargo haulers, the tool uses multi-variable optimization matrices to evaluate millions of airframe permutations in real-time.

Adjusting configurations dynamically scales three-dimensional aerodynamic projections, balance limits, tail volumes, and takeoff runs against localized structural weights.

---

## 🛠 How It Works

The core design philosophy of Micro-FAST is **scale-proportional dependency**. Instead of treating aircraft metrics (like wing chord, fuselage length, and tail surface areas) as isolated inputs, the engine treats them as an interconnected geometric web bounded by historical scale flight constants.

```
       [ USER INPUTS ]
  (Span, Power, Max Takeoff Run)
               │
               ▼
   ┌───────────────────────┐
   │ Optimization Matrix   │◄─── [ MULTI-VARIABLE BRUTE FORCE ]
   │ (Loops Chord, t/c,    │      Tests millions of combinations
   │  Fuselage Length)     │
   └───────────┬───────────┘
               │
               ▼
   ┌───────────────────────┐
   │ Flight Math Engine    │
   │ 1. Skin-friction CD0  │
   │ 2. V_stall Evaluation │
   │ 3. Ground Roll Accel  │
   └───────────┬───────────┘
               │
               ├────────────────────────┐
               ▼                        ▼
     [ VALID CONFIGURATIONS ]  [ FAILURES REJECTED ]
      Sorted by min V_stall     (Exceeds Max Takeoff Run
               │                 or Drag > Available Thrust)
               ▼
    ┌─────────────────────┐
    │     3D Canvas &     │
    │  Analytical Reports │
    └─────────────────────┘

```

When you click the optimization button, the system initiates a brute-force calculation matrix that dynamically slices through dimension bounds:

1. **Chord Sweep:** Steps through structural widths from $0.12\text{m}$ to $0.55\text{m}$.
2. **Thickness Tuning:** Steps through airfoil thickness ratios ($t/c$) from $5\%$ to $22\%$.
3. **Fuselage Length Scaling:** Sweeps overall structural leverage from $70\%$ to $95\%$ of the input wingspan.
4. **Sweep Angle Testing:** Explores aerodynamic quarter-chord sweeps depending on the airframe template chosen.

Any combination that produces a ground roll distance longer than your maximum allowable cutoff or lacks the static thrust necessary to overcome rolling resistance is immediately rejected. The remaining pool of valid designs is then sorted, surfacing the absolute safest variant (the airframe with the lowest stall speed) to display in the live 3D window.

---

## 📊 System UI Outputs Explained

The application populates an exhaustive flight matrix report across three diagnostic tabs. Here is exactly what every readout indicates:

### 1. Core Wing Dimensions

* **Wing Mean Chord:** The optimized average width of the wing profile from leading edge to trailing edge, balancing structural area against aspect-ratio-driven induced drag.
* **Wing Thickness Ratio ($t/c$):** The percentage height profile of the wing. Higher percentages improve internal structural beam strength but introduce harsh profile drag penalties at higher velocities.
* **Wing Quarter Sweep:** The structural backward or forward angle of the wing's $25\%$ chord line. Used primarily on high-speed templates to alter longitudinal pitch stability.
* **Dihedral / Anhedral Angle:** The upward or downward cant of the wings relative to the horizontal axis. Displays negative (Anhedral) for high-wing configurations to counterbalance heavy pendulum self-righting tendencies.

### 2. Tail Unit Proportional Specs

* **H-Stab Target Area / Span / Root Chord:** The exact physical dimensions required for the horizontal stabilizer. Calculated via scale pitch-restoring moment equations to ensure the nose does not tuck violently during power transitions.
* **V-Stab Target Area / Height / Root Chord:** The vertical fin requirements. Sized to combat lateral sideslip angles and protect the airframe against spiral instability or sudden yaw diverges.

### 3. Performance Matrices

* **Stall Velocity ($V_{\text{stall}}$):** The minimum true airspeed at which the airframe generates enough vertical lift to precisely match its total weight.
* **Fuselage Length:** The total nose-to-tail structure length, used as the geometric leverage baseline for stabilizing tail volumes.
* **Takeoff Ground Run:** The precise physical distance required to accelerate the aircraft from a dead stop to $120\%$ of its stall velocity under full power.

---

## ⚠️ Assumptions and Engineering Limitations

To ensure rapid calculation without lag, the software simplifies complex fluid dynamics using standard conceptual formulas. Users should design with these specific criteria in mind:

### Environmental & Atmospheric Constants

* **Sea-Level Fluid Density:** Air density ($\rho$) is locked at a standard $1.225 \text{ kg/m}^3$. It does not compensate for high density-altitude airfields, freezing field temperatures, or high-humidity lift degradation.
* **Ground Friction Roll:** The landing gear rolling resistance ($\mu$) is set to a constant $0.06$. This accurately models rough tarmac or short-trimmed runway grass but will underestimate takeoff rolls on deep dirt, sand, or overgrown fields.

### Aerodynamics & Profile Limitations

* **Linear Airfoil Scaling:** The baseline maximum lift coefficient ($C_{L_{\text{max}}}$) is assumed to follow an ideal linear lift curve slope based on the input NACA profile camber ($1.1 + (\text{Camber} \times 5)$). It does not account for low Reynolds number stalling where micro-scale air separation bubbles form early.
* **Empirical Induced Drag:** Calculated using a static Oswald efficiency rating ($e = 0.76$). Real-world wingtip vortices, tip shapes, and fuselage interference will cause minor efficiency deviations.
* **Tail Placement Assumptions:** Horizontal and vertical tails are assumed to have a clear, undisturbed airflow path. The software does not account for the main wing's downwash casting a wake over the rear surfaces at high angles of attack.

### Structural Weight Matrix

* **Balsa Density Framework:** Airframe empty mass formulas calculate structural weight based on an internal skeleton made of standard cured balsa wood ($130 \text{ kg/m}^3$). Carbon fiber layups or heavy foam-core builds will cause real-world weight variations.
* **Static Hardware Offset:** Includes a fixed $220\text{g}$ flat allowance for non-structural gear (servos, pushrods, film covering, and wiring). If heavy digital servos or thick fiberglass skin coatings are added, this weight budget must be manually compensated for via the input fields.

---

## 🤝 Areas for Contribution

Contributions are welcome to expand the depth and accuracy of this tool. If you want to contribute, please fork the repository and submit a pull request focusing on these targeted areas:

### 🚀 High-Priority Enhancements

* **Dynamic Atmospheric Profiles:**
* *Objective:* Expand the Environment tab to accept operational altitude and temperature inputs, calculating a dynamic fluid density value ($\rho$) rather than relying on standard sea-level defaults.


* **Real-time Reynolds Number Adjustments:**
* *Objective:* Inject an inline chord-and-velocity-based Reynolds check ($Re = \frac{\rho \cdot V \cdot c}{\mu}$) into the drag optimizer to penalize small-chord efficiency drops, yielding cleaner sub-scale drone estimations.


* **Multi-Element Weight Component Breakdown:**
* *Objective:* Replace the rigid payload/battery/structural inputs with a modular structural build picker (e.g., *Traditional Balsa, Solid EPS Foam, Carbon Composites, Thin-Wall 3D Prints*) to swap weight density curves automatically.


* **Extended Propulsion Physics:**
* *Objective:* Integrate propulsive scaling algorithms that map advance ratios ($J = \frac{V}{n \cdot D}$) and engine curves to model dynamic thrust loss as airspeed climbs, replacing the flat static thrust takeoff assumption.
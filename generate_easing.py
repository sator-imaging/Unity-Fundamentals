import math

PI = math.pi

def sine_in(x): return 1.0 - math.cos(x * PI / 2.0)
def sine_out(x): return math.sin(x * PI / 2.0)
def sine_in_out(x): return -(math.cos(PI * x) - 1.0) / 2.0

def circ_in(x): return 1.0 - math.sqrt(1.0 - x**2)
def circ_out(x): return math.sqrt(1.0 - (x - 1.0)**2)
def circ_in_out(x):
    if x < 0.5: return (1.0 - math.sqrt(1.0 - (2.0 * x)**2)) / 2.0
    else: return (math.sqrt(1.0 - (-2.0 * x + 2.0)**2) + 1.0) / 2.0

methods = {
    "CircIn": circ_in, "CircInOut": circ_in_out, "CircOut": circ_out,
    "SineIn": sine_in, "SineInOut": sine_in_out, "SineOut": sine_out,
}

for name in sorted(methods.keys()):
    vals = [methods[name](round(i * 0.1, 1)) for i in range(11)]
    formatted_vals = ", ".join([f"{v:.17g}" for v in vals])
    print(f'            {{ "{name}", new double[] {{ {formatted_vals} }} }},')

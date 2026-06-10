import threading
import urllib.request
import time
import urllib.parse

SERVER = "http://localhost:5000"
QUERIES = [
    "Inception",
    "Project Hail Mary",
    "The Devil Wears Prada",
    "Interstellar",
    "The Dark Knight",
    "Eternal Sunshine of the Spotless Mind",
    "Avatar The Way of Water",
    "Housemaid",
    "Primal Fear",
    "Before Sunrise",
    "La La Land",
    "The Notebook",
    "Obsession"
]

results = {
    "success":0,
    "error":0
    }

results_lock = threading.Lock();

def send_request(query):
    url = f"{SERVER}/search?query={urllib.parse.quote(query)}"
    try:
        start = time.time()
        response = urllib.request.urlopen(url)
        elapsed = time.time() - start
        with results_lock:
            results["success"] += 1
        print(f"[OK]\t '{query}' -> {elapsed:.3f}s (status: {response.status})")

    except Exception as e:
        with results_lock:
            results["error"] +=1
        print(f"[ERROR]\t '{query}' -> {e}")

threads = []
for query in QUERIES * 5:
    t = threading.Thread(target=send_request, args=(query,))
    threads.append(t)

print(f"Pokrecemo {len(threads)} zahteva .. \n")
start_total = time.time()

for t in threads:
    t.start()

for t in threads:
    t.join()

elapsed_total = time.time() - start_total

print(f"\n======== Rezultati ========")
print(f"  Uspesnih : {results['success']}")
print(f"  Gresaka  : {results['error']}")
print(f"  Ukupno   : {len(threads)}")
print(f"  Vreme    : {elapsed_total:.3f}s")
print(f"===========================")

DEBUG=1 # 1 - mixed-mode cross-language debugger
        # 2 - attach Python debugger

from clr import AddReference
AddReference("Algorithm")
from Algorithm import *

def attach_debugger():
    if DEBUG == 1:
        return
    import sys
    import time
    print("Waiting for debugger...")
    while not sys.gettrace():
        time.sleep(0.1)
    print(f"{sys.gettrace()} debugger attached")

class MyAlgorithm(Algorithm):

    def Initialize(self):
        self.SetCash(1000)

    def OnData(self, data):
        attach_debugger()
        cash = self.Portfolio.Cash
        print(f"OnData: data = {data}, cash = {cash}")
